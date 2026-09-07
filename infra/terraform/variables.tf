variable "name" {
  description = "Prefix for every resource name."
  type        = string
  default     = "requestdesk"
}

variable "region" {
  description = "AWS region."
  type        = string
  default     = "us-east-1"
}

variable "vpc_cidr" {
  description = "CIDR block for the VPC."
  type        = string
  default     = "10.40.0.0/16"
}

variable "api_image" {
  description = "Container image for the API, for example ghcr.io/eric-patton/requestdesk-api:latest."
  type        = string
}

variable "web_image" {
  description = "Container image for the nginx front end."
  type        = string
}

variable "api_cpu" {
  description = "Fargate CPU units for the API task."
  type        = number
  default     = 512
}

variable "api_memory" {
  description = "Fargate memory (MiB) for the API task."
  type        = number
  default     = 1024
}

variable "web_cpu" {
  type    = number
  default = 256
}

variable "web_memory" {
  type    = number
  default = 512
}

variable "desired_count" {
  description = "Tasks per service."
  type        = number
  default     = 1
}

variable "db_instance_class" {
  description = "RDS instance class. db.t4g.micro is the smallest Graviton class."
  type        = string
  default     = "db.t4g.micro"
}

variable "db_allocated_storage" {
  description = "RDS storage in GiB."
  type        = number
  default     = 20
}

variable "demo_mode" {
  description = "Seed synthetic data and reset it hourly."
  type        = bool
  default     = true
}

variable "log_retention_days" {
  type    = number
  default = 30
}
