output "url" {
  description = "Where the application answers."
  value       = "http://${aws_lb.this.dns_name}"
}

output "database_endpoint" {
  description = "RDS endpoint, private to the VPC."
  value       = aws_db_instance.this.address
}

output "attachments_bucket" {
  value = aws_s3_bucket.attachments.bucket
}

output "ecs_cluster" {
  value = aws_ecs_cluster.this.name
}
