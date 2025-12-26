# Multi-stage Dockerfile for SXG Evaluation Platform
FROM mcr.microsoft.com/cbl-mariner/base/python:3.9 as builder

# Set locale and encoding environment variables using C.UTF-8 (always available)
ENV LANG=C.UTF-8 \
    LC_ALL=C.UTF-8 \
    PYTHONIOENCODING=utf-8 \
    PYTHONUNBUFFERED=1 \
    PYTHONWARNINGS=ignore

# Set working directory
WORKDIR /app

# Install system dependencies (removing unavailable locale packages)
RUN tdnf update -y && \
    tdnf install -y \
    gcc \
    g++ \
    python3-pip \
    && tdnf clean all

# Upgrade pip to latest version
RUN python -m pip install --upgrade pip

# Copy requirements first to leverage Docker cache
COPY requirements.txt .

# Install Python dependencies with explicit UTF-8 encoding
RUN PYTHONIOENCODING=utf-8 LC_ALL=C.UTF-8 LANG=C.UTF-8 \
    pip install --no-cache-dir --user -r requirements.txt

# Production stage
FROM mcr.microsoft.com/cbl-mariner/base/python:3.9

# Set locale and encoding environment variables using C.UTF-8
ENV LANG=C.UTF-8 \
    LC_ALL=C.UTF-8 \
    PYTHONIOENCODING=utf-8 \
    PYTHONUNBUFFERED=1 \
    PYTHONWARNINGS=ignore

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
ENV PATH=/home/evaluser/.local/bin:$PATH \
    PYTHONPATH=/app/src

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
