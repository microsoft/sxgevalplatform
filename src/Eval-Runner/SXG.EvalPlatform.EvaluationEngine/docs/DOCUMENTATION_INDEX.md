# 📚 Documentation Index

## 🎯 Welcome to SXG Evaluation Platform Documentation

This is your comprehensive guide to the SXG Evaluation Platform Evaluation Engine - a high-performance, cloud-native Python application for AI agent evaluation at scale.

## 🚀 Quick Navigation

### 🏁 Getting Started
- **[README.md](../README.md)** - Main project overview and quick start guide
- **[QUICKSTART.md](QUICKSTART.md)** - Step-by-step setup instructions  
- **[PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md)** - Detailed project architecture and components

### 🔧 Development & Contributing
- **[CONTRIBUTING.md](CONTRIBUTING.md)** - Development setup, coding standards, and contribution process
- **[API_DOCUMENTATION.md](API_DOCUMENTATION.md)** - Comprehensive API reference and interfaces

### 🚀 Performance & Optimization
- **[PERFORMANCE_GUIDE.md](PERFORMANCE_GUIDE.md)** - Performance optimization, benchmarks, and tuning

### 🛠️ Operations & Deployment
- **[DEPLOYMENT.md](DEPLOYMENT.md)** - Azure Container Apps deployment guide
- **[DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md)** - Comprehensive deployment instructions
- **[TROUBLESHOOTING_GUIDE.md](TROUBLESHOOTING_GUIDE.md)** - Common issues, debugging, and solutions

### 📊 Evaluation & Metrics
- **[EVALUATORS_README.md](EVALUATORS_README.md)** - Complete guide to 20+ evaluation metrics

### ☁️ Azure Integration
- **[AZURE_AI_FOUNDRY_MANAGED_IDENTITY.md](AZURE_AI_FOUNDRY_MANAGED_IDENTITY.md)** - Azure AI Foundry integration
- **[AZURE_STORAGE_MANAGED_IDENTITY.md](AZURE_STORAGE_MANAGED_IDENTITY.md)** - Azure Storage managed identity setup
- **[MANAGED_IDENTITY_GUIDE.md](MANAGED_IDENTITY_GUIDE.md)** - Complete managed identity configuration

## 📖 Documentation Categories

### 📋 **User Documentation**
Perfect for users who want to understand and use the platform:

| Document | Purpose | Audience |
|----------|---------|----------|
| [README.md](../README.md) | Project overview, features, quick start | All users |
| [QUICKSTART.md](QUICKSTART.md) | Step-by-step setup guide | New users |  
| [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md) | Architecture and component details | Technical users |
| [EVALUATORS_README.md](EVALUATORS_README.md) | Complete metrics documentation | Data scientists, ML engineers |

### 🔧 **Developer Documentation**
Essential for developers working on the codebase:

| Document | Purpose | Audience |
|----------|---------|----------|
| [CONTRIBUTING.md](CONTRIBUTING.md) | Development setup, standards, process | Contributors |
| [API_DOCUMENTATION.md](API_DOCUMENTATION.md) | API interfaces and schemas | API consumers, developers |

### 🚀 **Operations Documentation**  
Critical for deployment and production management:

| Document | Purpose | Audience |
|----------|---------|----------|
| [DEPLOYMENT.md](DEPLOYMENT.md) | Azure Container Apps deployment | DevOps, SRE |
| [PERFORMANCE_GUIDE.md](PERFORMANCE_GUIDE.md) | Performance tuning and monitoring | SRE, Performance engineers |
| [TROUBLESHOOTING_GUIDE.md](TROUBLESHOOTING_GUIDE.md) | Issue diagnosis and resolution | Support, Operations |
| [MANAGED_IDENTITY_GUIDE.md](MANAGED_IDENTITY_GUIDE.md) | Azure authentication setup | Cloud engineers |

## 🎯 Documentation by Use Case

### 🔰 **"I'm new to this project"**
Start here for a smooth onboarding experience:
1. **[README.md](../README.md)** - Get the big picture
2. **[PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md)** - Understand the architecture  
3. **[QUICKSTART.md](QUICKSTART.md)** - Get up and running
4. **[EVALUATORS_README.md](EVALUATORS_README.md)** - Learn about available metrics

### 💻 **"I want to contribute code"**
Everything you need for development:
1. **[CONTRIBUTING.md](CONTRIBUTING.md)** - Development setup and standards
2. **[API_DOCUMENTATION.md](API_DOCUMENTATION.md)** - Understand the APIs
3. **[PERFORMANCE_GUIDE.md](PERFORMANCE_GUIDE.md)** - Performance considerations
4. **[TROUBLESHOOTING_GUIDE.md](TROUBLESHOOTING_GUIDE.md)** - Debug like a pro

### 🚀 **"I need to deploy this"**
Production deployment resources:
1. **[DEPLOYMENT.md](DEPLOYMENT.md)** - Step-by-step deployment
2. **[MANAGED_IDENTITY_GUIDE.md](MANAGED_IDENTITY_GUIDE.md)** - Azure authentication
3. **[PERFORMANCE_GUIDE.md](PERFORMANCE_GUIDE.md)** - Production tuning
4. **[TROUBLESHOOTING_GUIDE.md](TROUBLESHOOTING_GUIDE.md)** - Operations support

### 🔧 **"I need to optimize performance"**
Performance and optimization resources:
1. **[PERFORMANCE_GUIDE.md](PERFORMANCE_GUIDE.md)** - Tuning parameters and benchmarks
2. **[API_DOCUMENTATION.md](API_DOCUMENTATION.md)** - Performance considerations
3. **[TROUBLESHOOTING_GUIDE.md](TROUBLESHOOTING_GUIDE.md)** - Performance troubleshooting

### 🔍 **"I'm troubleshooting an issue"**
Diagnostic and resolution resources:
1. **[TROUBLESHOOTING_GUIDE.md](TROUBLESHOOTING_GUIDE.md)** - Comprehensive troubleshooting
2. **[PERFORMANCE_GUIDE.md](PERFORMANCE_GUIDE.md)** - Performance issues
3. **[API_DOCUMENTATION.md](API_DOCUMENTATION.md)** - API error codes
4. **[DEPLOYMENT.md](DEPLOYMENT.md)** - Deployment issues

## 🔗 Quick Reference Links

### 📊 **Key Performance Metrics**
- **60% Performance Improvement**: Concurrent vs sequential processing
- **3x Dataset Concurrency**: Multiple items processed simultaneously  
- **8x Metric Concurrency**: Parallel metric evaluation
- **20+ Evaluation Metrics**: Across 4 categories (Agentic, RAG, Safety, Similarity)

### 🏗️ **Architecture Highlights**
- **Queue-based Processing**: Azure Storage Queue integration
- **Concurrent Evaluation**: Optimized async/await implementation
- **Connection Pooling**: HTTP and Azure Storage optimization
- **Managed Identity**: Secure Azure resource access
- **Comprehensive Logging**: Structured logging with performance metrics

### 🔧 **Key Configuration**
```json
{
  "Evaluation": {
    "DatasetConcurrency": 3,
    "MetricConcurrency": 8,
    "MetricTimeoutSeconds": 30
  },
  "HttpClient": {
    "MaxConnections": 20,
    "ConnectionTimeoutSeconds": 30
  }
}
```

## 📈 **Recent Updates & Improvements**

### ✅ **Completed Optimizations** (Latest)
- **Concurrent Processing**: 60% performance improvement implemented
- **HTTP Connection Pooling**: Persistent connections with lifecycle management
- **Azure Storage Optimization**: Enhanced connection pooling and timeouts
- **Resource Management**: Proper cleanup and graceful shutdown
- **Performance Monitoring**: Built-in timing and structured logging

### 📋 **Documentation Enhancements**
- **API Documentation**: Comprehensive interface documentation
- **Performance Guide**: Detailed tuning and monitoring guide
- **Troubleshooting Guide**: Complete diagnostic and resolution procedures
- **Contributing Guide**: Development standards and contribution process

## 🆘 **Need Help?**

### 🔍 **Finding Information**
1. **Search this index** for your topic
2. **Check the README** for general information
3. **Use the troubleshooting guide** for issues
4. **Review API documentation** for technical details

### 📞 **Getting Support**
- **GitHub Issues**: Report bugs or request features
- **GitHub Discussions**: Ask questions or start discussions
- **Team Contacts**: Reach out to the development team
- **Azure Support**: For Azure service-related issues

## 📝 **Documentation Maintenance**

This documentation is actively maintained and regularly updated. Each document includes:
- **Last Updated**: Date of most recent updates
- **Version Information**: Compatibility information
- **Related Documents**: Cross-references to related content
- **Examples**: Practical usage examples

### 🔄 **Contributing to Documentation**
Help us improve the documentation:
1. **Identify gaps**: Missing information or outdated content
2. **Submit improvements**: Pull requests with documentation updates
3. **Report issues**: Flag unclear or incorrect documentation
4. **Share feedback**: Suggest improvements or new content

---

## 📚 **Complete Document List**

### Core Documentation
- [README.md](README.md) - Main project documentation
- [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md) - Architecture overview
- [QUICKSTART.md](QUICKSTART.md) - Quick setup guide

### Development & API
- [CONTRIBUTING.md](CONTRIBUTING.md) - Contributor guide
- [API_DOCUMENTATION.md](API_DOCUMENTATION.md) - API reference

### Performance & Optimization  
- [PERFORMANCE_GUIDE.md](PERFORMANCE_GUIDE.md) - Performance guide

### Operations & Deployment
- [DEPLOYMENT.md](DEPLOYMENT.md) - Azure deployment guide
- [DEPLOYMENT_GUIDE.md](DEPLOYMENT_GUIDE.md) - Comprehensive deployment
- [TROUBLESHOOTING_GUIDE.md](TROUBLESHOOTING_GUIDE.md) - Issue resolution

### Evaluation & Metrics
- [EVALUATORS_README.md](EVALUATORS_README.md) - Metrics documentation

### Azure Integration
- [AZURE_AI_FOUNDRY_MANAGED_IDENTITY.md](AZURE_AI_FOUNDRY_MANAGED_IDENTITY.md) - AI Foundry setup
- [AZURE_STORAGE_MANAGED_IDENTITY.md](AZURE_STORAGE_MANAGED_IDENTITY.md) - Storage setup
- [MANAGED_IDENTITY_GUIDE.md](MANAGED_IDENTITY_GUIDE.md) - Complete identity guide

---

**🎉 Welcome to the SXG Evaluation Platform! Start with the [README.md](README.md) for your journey into high-performance AI evaluation.**