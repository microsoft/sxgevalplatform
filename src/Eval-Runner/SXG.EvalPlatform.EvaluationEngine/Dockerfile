# Multi-stage Dockerfile for SXG Evaluation Platform
FROM mcr.microsoft.com/cbl-mariner/base/python:3.9 as builder

# Set working directory
WORKDIR /app

# Install system dependencies
RUN tdnf install -y \
    gcc \
    g++ \
    && tdnf clean all

# Copy requirements first to leverage Docker cache
COPY requirements.txt .

# Install Python dependencies
RUN pip install --no-cache-dir --user -r requirements.txt

# Production stage
FROM mcr.microsoft.com/cbl-mariner/base/python:3.11

# Set working directory
WORKDIR /app

# Create non-root user for security
RUN groupadd -r evaluser && useradd -r -g evaluser evaluser

# Copy installed packages from builder stage
COPY --from=builder /root/.local /home/evaluser/.local

# Copy application code and all configuration files
COPY src/ ./src/
COPY appsettings*.json ./

# Set environment variables
ENV PATH=/home/evaluser/.local/bin:$PATH
ENV PYTHONPATH=/app/src
ENV PYTHONUNBUFFERED=1

# Set ownership
RUN chown -R evaluser:evaluser /app

# Switch to non-root user
USER evaluser

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD python -c "import sys; sys.exit(0)" || exit 1

# Expose port (if needed for health checks)
EXPOSE 8080

# Command to run the application
CMD ["python", "src/main.py"]
