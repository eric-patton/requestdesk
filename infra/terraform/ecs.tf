# ECS on Fargate: one cluster, two services (api, web), an application load balancer in front.
# /api/* and the API's operational endpoints route to the API service; everything else to nginx.

resource "aws_ecs_cluster" "this" {
  name = var.name

  setting {
    name  = "containerInsights"
    value = "enabled"
  }
}

resource "aws_cloudwatch_log_group" "api" {
  name              = "/ecs/${var.name}/api"
  retention_in_days = var.log_retention_days
}

resource "aws_cloudwatch_log_group" "web" {
  name              = "/ecs/${var.name}/web"
  retention_in_days = var.log_retention_days
}

# IAM. The execution role pulls images and reads the two secrets; the task role is what the API
# runs as and may only touch the attachments bucket.

data "aws_iam_policy_document" "ecs_assume" {
  statement {
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["ecs-tasks.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "execution" {
  name               = "${var.name}-ecs-execution"
  assume_role_policy = data.aws_iam_policy_document.ecs_assume.json
}

resource "aws_iam_role_policy_attachment" "execution" {
  role       = aws_iam_role.execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

data "aws_iam_policy_document" "execution_secrets" {
  statement {
    actions = ["secretsmanager:GetSecretValue"]
    resources = [
      aws_secretsmanager_secret.connection_string.arn,
      aws_secretsmanager_secret.jwt_signing_key.arn,
    ]
  }
}

resource "aws_iam_role_policy" "execution_secrets" {
  name   = "read-secrets"
  role   = aws_iam_role.execution.id
  policy = data.aws_iam_policy_document.execution_secrets.json
}

resource "aws_iam_role" "api_task" {
  name               = "${var.name}-api-task"
  assume_role_policy = data.aws_iam_policy_document.ecs_assume.json
}

data "aws_iam_policy_document" "api_task" {
  statement {
    actions   = ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"]
    resources = ["${aws_s3_bucket.attachments.arn}/*"]
  }

  statement {
    actions   = ["s3:ListBucket"]
    resources = [aws_s3_bucket.attachments.arn]
  }
}

resource "aws_iam_role_policy" "api_task" {
  name   = "attachments-bucket"
  role   = aws_iam_role.api_task.id
  policy = data.aws_iam_policy_document.api_task.json
}

# Task definitions.

resource "aws_ecs_task_definition" "api" {
  family                   = "${var.name}-api"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.api_cpu
  memory                   = var.api_memory
  execution_role_arn       = aws_iam_role.execution.arn
  task_role_arn            = aws_iam_role.api_task.arn

  runtime_platform {
    operating_system_family = "LINUX"
    cpu_architecture        = "X86_64"
  }

  container_definitions = jsonencode([
    {
      name      = "api"
      image     = var.api_image
      essential = true

      portMappings = [{ containerPort = 8080, protocol = "tcp" }]

      environment = [
        { name = "ASPNETCORE_ENVIRONMENT", value = "Production" },
        { name = "ASPNETCORE_URLS", value = "http://+:8080" },
        { name = "Demo__Enabled", value = tostring(var.demo_mode) },
        { name = "Demo__ResetIntervalMinutes", value = "60" },
        { name = "Storage__Root", value = "/app/var/attachments" },
        { name = "Attachments__Bucket", value = aws_s3_bucket.attachments.bucket },
      ]

      secrets = [
        { name = "ConnectionStrings__Default", valueFrom = aws_secretsmanager_secret.connection_string.arn },
        { name = "Jwt__SigningKey", valueFrom = aws_secretsmanager_secret.jwt_signing_key.arn },
      ]

      healthCheck = {
        command     = ["CMD-SHELL", "wget -q -O /dev/null http://localhost:8080/health/live || exit 1"]
        interval    = 15
        timeout     = 5
        retries     = 3
        startPeriod = 30
      }

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = aws_cloudwatch_log_group.api.name
          awslogs-region        = var.region
          awslogs-stream-prefix = "api"
        }
      }
    }
  ])
}

resource "aws_ecs_task_definition" "web" {
  family                   = "${var.name}-web"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.web_cpu
  memory                   = var.web_memory
  execution_role_arn       = aws_iam_role.execution.arn

  runtime_platform {
    operating_system_family = "LINUX"
    cpu_architecture        = "X86_64"
  }

  container_definitions = jsonencode([
    {
      name      = "web"
      image     = var.web_image
      essential = true

      portMappings = [{ containerPort = 80, protocol = "tcp" }]

      # nginx proxies /api to the load balancer, which routes it to the API service. Same origin
      # for the browser, no CORS, and the API is never exposed except through the balancer.
      environment = [
        { name = "API_UPSTREAM", value = "http://${aws_lb.this.dns_name}" },
        { name = "DNS_RESOLVER", value = "169.254.169.253" },
      ]

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = aws_cloudwatch_log_group.web.name
          awslogs-region        = var.region
          awslogs-stream-prefix = "web"
        }
      }
    }
  ])
}

# Services.

resource "aws_ecs_service" "api" {
  name            = "${var.name}-api"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.api.arn
  desired_count   = var.desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = aws_subnet.private[*].id
    security_groups  = [aws_security_group.tasks.id]
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.api.arn
    container_name   = "api"
    container_port   = 8080
  }

  deployment_minimum_healthy_percent = 100
  deployment_maximum_percent         = 200
  health_check_grace_period_seconds  = 60

  depends_on = [aws_lb_listener.http]
}

resource "aws_ecs_service" "web" {
  name            = "${var.name}-web"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.web.arn
  desired_count   = var.desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = aws_subnet.private[*].id
    security_groups  = [aws_security_group.tasks.id]
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.web.arn
    container_name   = "web"
    container_port   = 80
  }

  deployment_minimum_healthy_percent = 100
  deployment_maximum_percent         = 200

  depends_on = [aws_lb_listener.http]
}
