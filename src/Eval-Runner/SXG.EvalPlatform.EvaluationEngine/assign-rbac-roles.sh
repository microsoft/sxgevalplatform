#!/bin/bash

# RBAC Role Assignment Script for Container App Managed Identity

set -e

# Configuration
CONTAINER_APP_NAME="eval-framework-app"
RESOURCE_GROUP="rg-sxg-agent-evaluation-platform"
OPENAI_ACCOUNT_NAME="evalplatform"

# Function to print colored output
print_status() {
    echo -e "\033[0;34m[INFO]\033[0m $1"
}

print_success() {
    echo -e "\033[0;32m[SUCCESS]\033[0m $1"
}

print_error() {
    echo -e "\033[0;31m[ERROR]\033[0m $1"
}

print_warning() {
    echo -e "\033[1;33m[WARNING]\033[0m $1"
}

# Check parameters
if [ $# -ne 2 ]; then
    echo "Usage: $0 <storage-account-name> <subscription-id>"
    echo "Example: $0 mystorageaccount d2ef7484-d847-4ca9-88be-d2d9f2a8a50f"
    exit 1
fi

STORAGE_ACCOUNT_NAME=$1
SUBSCRIPTION_ID=$2

print_status "🔐 Configuring RBAC permissions for Container App managed identity..."

# Get the Container App's managed identity principal ID
print_status "Getting Container App managed identity..."
PRINCIPAL_ID=$(az containerapp show --name $CONTAINER_APP_NAME --resource-group $RESOURCE_GROUP --query identity.principalId -o tsv)

if [ -z "$PRINCIPAL_ID" ] || [ "$PRINCIPAL_ID" = "null" ]; then
    print_error "Failed to get managed identity principal ID. Ensure the Container App has system-assigned managed identity enabled."
    exit 1
fi

print_success "Found managed identity principal ID: $PRINCIPAL_ID"

# Storage Account Permissions
print_status "📦 Assigning Storage Account permissions..."

STORAGE_SCOPE="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Storage/storageAccounts/$STORAGE_ACCOUNT_NAME"

# Storage Queue Data Contributor
print_status "Assigning Storage Queue Data Contributor role..."
if az role assignment create \
    --assignee $PRINCIPAL_ID \
    --role "Storage Queue Data Contributor" \
    --scope $STORAGE_SCOPE > /dev/null 2>&1; then
    print_success "Storage Queue Data Contributor role assigned"
else
    print_error "Failed to assign Storage Queue Data Contributor role"
fi

# Storage Blob Data Contributor
print_status "Assigning Storage Blob Data Contributor role..."
if az role assignment create \
    --assignee $PRINCIPAL_ID \
    --role "Storage Blob Data Contributor" \
    --scope $STORAGE_SCOPE > /dev/null 2>&1; then
    print_success "Storage Blob Data Contributor role assigned"
else
    print_error "Failed to assign Storage Blob Data Contributor role"
fi

# Azure OpenAI Permissions
print_status "🤖 Assigning Azure OpenAI permissions..."

OPENAI_SCOPE="/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.CognitiveServices/accounts/$OPENAI_ACCOUNT_NAME"

# Cognitive Services OpenAI User
print_status "Assigning Cognitive Services OpenAI User role..."
if az role assignment create \
    --assignee $PRINCIPAL_ID \
    --role "Cognitive Services OpenAI User" \
    --scope $OPENAI_SCOPE > /dev/null 2>&1; then
    print_success "Cognitive Services OpenAI User role assigned"
else
    print_error "Failed to assign Cognitive Services OpenAI User role"
fi

# Verify role assignments
print_status "🔍 Verifying role assignments..."
echo "Current role assignments for managed identity:"
az role assignment list --assignee $PRINCIPAL_ID --output table

print_success "✅ RBAC configuration completed!"
echo "The Container App can now access:"
echo "  • Azure Storage queues and blobs"
echo "  • Azure OpenAI service"
print_warning "Note: Role propagation may take a few minutes to take effect."