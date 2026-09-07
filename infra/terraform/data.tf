# PostgreSQL on RDS, attachments in S3, and the two secrets the API needs at runtime.

resource "random_password" "database" {
  length  = 32
  special = false
}

resource "random_password" "jwt_signing_key" {
  length  = 64
  special = false
}

resource "aws_db_subnet_group" "this" {
  name       = "${var.name}-db"
  subnet_ids = aws_subnet.private[*].id
}

resource "aws_db_instance" "this" {
  identifier = "${var.name}-db"

  engine         = "postgres"
  engine_version = "17"
  instance_class = var.db_instance_class

  allocated_storage     = var.db_allocated_storage
  max_allocated_storage = var.db_allocated_storage * 2
  storage_type          = "gp3"
  storage_encrypted     = true

  db_name  = "requestdesk"
  username = "requestdesk"
  password = random_password.database.result

  db_subnet_group_name   = aws_db_subnet_group.this.name
  vpc_security_group_ids = [aws_security_group.database.id]
  publicly_accessible    = false
  multi_az               = false

  backup_retention_period = 7
  deletion_protection     = false
  skip_final_snapshot     = true
  apply_immediately       = true

  performance_insights_enabled = false
}

# Attachments. Private, versioned, encrypted, and no public access under any circumstances. The
# API's IFileStorage abstraction is shaped so an S3 implementation targets exactly this bucket.
resource "aws_s3_bucket" "attachments" {
  bucket_prefix = "${var.name}-attachments-"
  force_destroy = true
}

resource "aws_s3_bucket_versioning" "attachments" {
  bucket = aws_s3_bucket.attachments.id

  versioning_configuration {
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "attachments" {
  bucket = aws_s3_bucket.attachments.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
  }
}

resource "aws_s3_bucket_public_access_block" "attachments" {
  bucket = aws_s3_bucket.attachments.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

# Secrets Manager holds the connection string and the JWT key. The task definition references them
# by ARN; the values never appear in the task definition, the console, or this repository.
resource "aws_secretsmanager_secret" "connection_string" {
  name_prefix = "${var.name}/connection-string-"
}

resource "aws_secretsmanager_secret_version" "connection_string" {
  secret_id = aws_secretsmanager_secret.connection_string.id
  secret_string = join(";", [
    "Host=${aws_db_instance.this.address}",
    "Port=${aws_db_instance.this.port}",
    "Database=${aws_db_instance.this.db_name}",
    "Username=${aws_db_instance.this.username}",
    "Password=${random_password.database.result}",
    "SSL Mode=Require",
  ])
}

resource "aws_secretsmanager_secret" "jwt_signing_key" {
  name_prefix = "${var.name}/jwt-signing-key-"
}

resource "aws_secretsmanager_secret_version" "jwt_signing_key" {
  secret_id     = aws_secretsmanager_secret.jwt_signing_key.id
  secret_string = random_password.jwt_signing_key.result
}
