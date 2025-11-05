#!/bin/bash

# Azure Container App Deployment Script
# This script builds, pushes, and deploys the evaluation engine to Azure Container Apps

set -e  # Exit on any error

# Configuration
RESOURCE_GROUP="rg-sxg-agent-evaluation-platform"
CONTAINER_APP_NAME="eval-framework-app"
CONTAINER_REGISTRY="evalplatformregistry"
IMAGE_NAME="eval-framework-app"
IMAGE_TAG="latest"
TEMPLATE_FILE="deployment/container-app-template.json"
PARAMETERS_FILE="deployment/container-app-parameters.json"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to check if required tools are installed
check_prerequisites() {
    print_status "Checking prerequisites..."
    
    # Check if Azure CLI is installed
    if ! command -v az &> /dev/null; then
        print_error "Azure CLI is not installed. Please install it first."
        exit 1
    fi
    
    # Check if Docker is installed
    if ! command -v docker &> /dev/null; then
        print_error "Docker is not installed. Please install it first."
        exit 1
    fi
    
    # Check if logged into Azure
    if ! az account show &> /dev/null; then
        print_error "Not logged into Azure. Please run 'az login' first."
        exit 1
    fi
    
    print_success "All prerequisites met!"
}

# Function to build Docker image
build_image() {
    print_status "Building Docker image..."
    
    # Get the full registry login server name
    REGISTRY_SERVER=$(az acr show --name $CONTAINER_REGISTRY --resource-group $RESOURCE_GROUP --query loginServer --output tsv)
    FULL_IMAGE_NAME="$REGISTRY_SERVER/$IMAGE_NAME:$IMAGE_TAG"
    
    # Build the Docker image
    docker build -t $FULL_IMAGE_NAME .
    
    if [ $? -eq 0 ]; then
        print_success "Docker image built successfully: $FULL_IMAGE_NAME"
    else
        print_error "Failed to build Docker image"
        exit 1
    fi
}

# Function to push image to ACR
push_image() {
    print_status "Pushing image to Azure Container Registry..."
    
    # Login to ACR
    az acr login --name $CONTAINER_REGISTRY
    
    # Get the full registry login server name
    REGISTRY_SERVER=$(az acr show --name $CONTAINER_REGISTRY --resource-group $RESOURCE_GROUP --query loginServer --output tsv)
    FULL_IMAGE_NAME="$REGISTRY_SERVER/$IMAGE_NAME:$IMAGE_TAG"
    
    # Push the image
    docker push $FULL_IMAGE_NAME
    
    if [ $? -eq 0 ]; then
        print_success "Image pushed successfully to ACR"
    else
        print_error "Failed to push image to ACR"
        exit 1
    fi
}

# Function to update parameters file with current image tag
update_parameters() {
    print_status "Updating deployment parameters..."
    
    # Update the image tag in parameters file if needed
    if [ "$IMAGE_TAG" != "latest" ]; then
        # Create a temporary parameters file with updated image tag
        jq --arg tag "$IMAGE_TAG" '.parameters.imageTag.value = $tag' $PARAMETERS_FILE > temp_parameters.json
        mv temp_parameters.json $PARAMETERS_FILE
        print_success "Parameters updated with image tag: $IMAGE_TAG"
    fi
}

# Function to deploy to Container Apps
deploy_container_app() {
    print_status "Deploying to Azure Container Apps..."
    
    # Deploy using ARM template
    DEPLOYMENT_NAME="eval-framework-deployment-$(date +%Y%m%d-%H%M%S)"
    
    az deployment group create \
        --resource-group $RESOURCE_GROUP \
        --template-file $TEMPLATE_FILE \
        --parameters @$PARAMETERS_FILE \
        --name $DEPLOYMENT_NAME \
        --verbose
    
    if [ $? -eq 0 ]; then
        print_success "Container App deployed successfully!"
        
        # Get the deployment outputs
        print_status "Getting deployment information..."
        CONTAINER_APP_FQDN=$(az deployment group show \
            --resource-group $RESOURCE_GROUP \
            --name $DEPLOYMENT_NAME \
            --query "properties.outputs.containerAppFqdn.value" \
            --output tsv)
        
        if [ ! -z "$CONTAINER_APP_FQDN" ]; then
            print_success "Container App URL: https://$CONTAINER_APP_FQDN"
        fi
    else
        print_error "Failed to deploy Container App"
        exit 1
    fi
}

# Function to show logs
show_logs() {
    print_status "Fetching recent logs from Container App..."
    
    az containerapp logs show \
        --name $CONTAINER_APP_NAME \
        --resource-group $RESOURCE_GROUP \
        --follow false \
        --tail 50
}

# Function to show app status
show_status() {
    print_status "Getting Container App status..."
    
    az containerapp show \
        --name $CONTAINER_APP_NAME \
        --resource-group $RESOURCE_GROUP \
        --query "{name:name,status:properties.provisioningState,replicas:properties.template.scale.minReplicas,fqdn:properties.configuration.ingress.fqdn}" \
        --output table
}

# Main deployment function
main() {
    print_status "Starting deployment of Evaluation Engine to Azure Container Apps..."
    echo "Configuration:"
    echo "  Resource Group: $RESOURCE_GROUP"
    echo "  Container App: $CONTAINER_APP_NAME" 
    echo "  Registry: $CONTAINER_REGISTRY"
    echo "  Image: $IMAGE_NAME:$IMAGE_TAG"
    echo ""
    
    # Check if parameters file needs to be updated
    if [ ! -f "$PARAMETERS_FILE" ]; then
        print_error "Parameters file not found: $PARAMETERS_FILE"
        print_error "Please update the parameters file with your actual configuration values."
        exit 1
    fi
    
    # Check for placeholder values in parameters
    if grep -q "REPLACE_WITH_ACTUAL" "$PARAMETERS_FILE"; then
        print_error "Parameters file contains placeholder values."
        print_error "Please update $PARAMETERS_FILE with actual configuration values before deploying."
        exit 1
    fi
    
    check_prerequisites
    build_image
    push_image
    update_parameters
    deploy_container_app
    
    print_success "Deployment completed successfully!"
    echo ""
    print_status "Next steps:"
    echo "  1. Assign RBAC roles: ./assign-rbac-roles.sh <storage-account-name> <subscription-id>"
    echo "  2. Check app status: ./deploy.sh status"
    echo "  3. View logs: ./deploy.sh logs"
    echo "  4. Monitor in Azure Portal"
    echo ""
    print_warning "⚠️  Don't forget to assign RBAC permissions for managed identity access!"
}

# Handle command line arguments
case "${1:-deploy}" in
    "deploy")
        main
        ;;
    "build")
        check_prerequisites
        build_image
        ;;
    "push")
        check_prerequisites
        push_image
        ;;
    "status")
        show_status
        ;;
    "logs")
        show_logs
        ;;
    "help"|"--help"|"-h")
        echo "Usage: $0 [command]"
        echo ""
        echo "Commands:"
        echo "  deploy    - Full deployment (build, push, deploy) [default]"
        echo "  build     - Build Docker image only"
        echo "  push      - Push image to ACR only"
        echo "  status    - Show Container App status"
        echo "  logs      - Show Container App logs"
        echo "  help      - Show this help message"
        ;;
    *)
        print_error "Unknown command: $1"
        echo "Run '$0 help' for usage information"
        exit 1
        ;;
esac