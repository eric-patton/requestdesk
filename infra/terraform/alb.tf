# The public entry point. HTTP only in this reference module; a real deployment adds an ACM
# certificate and an HTTPS listener with a redirect, which is a dozen lines and a domain name.

resource "aws_lb" "this" {
  name               = var.name
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets            = aws_subnet.public[*].id

  idle_timeout = 60
}

resource "aws_lb_target_group" "api" {
  name        = "${var.name}-api"
  port        = 8080
  protocol    = "HTTP"
  target_type = "ip"
  vpc_id      = aws_vpc.this.id

  deregistration_delay = 15

  health_check {
    path                = "/health/live"
    interval            = 15
    timeout             = 5
    healthy_threshold   = 2
    unhealthy_threshold = 3
    matcher             = "200"
  }
}

resource "aws_lb_target_group" "web" {
  name        = "${var.name}-web"
  port        = 80
  protocol    = "HTTP"
  target_type = "ip"
  vpc_id      = aws_vpc.this.id

  deregistration_delay = 15

  health_check {
    path                = "/"
    interval            = 15
    timeout             = 5
    healthy_threshold   = 2
    unhealthy_threshold = 3
    matcher             = "200"
  }
}

resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.this.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.web.arn
  }
}

resource "aws_lb_listener_rule" "api" {
  listener_arn = aws_lb_listener.http.arn
  priority     = 10

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.api.arn
  }

  condition {
    path_pattern {
      values = ["/api/*", "/health", "/health/*", "/openapi/*", "/scalar", "/scalar/*"]
    }
  }
}
