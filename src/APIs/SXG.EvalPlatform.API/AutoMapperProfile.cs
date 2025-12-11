using AutoMapper;
using Sxg.EvalPlatform.API.Storage.Entities;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Models.Dtos;

namespace SxgEvalPlatformApi
{
    /// <summary>
    /// Optimized AutoMapper profile for mapping DTOs to entities and vice versa
    /// </summary>
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            ConfigureMappings();
        }

        private void ConfigureMappings()
        {
            ConfigureMetricsConfigurationMappings();
            ConfigureDataSetMappings();
            ConfigureCollectionMappings();
        }
        
        private void ConfigureMetricsConfigurationMappings()
        {
            CreateMap<CreateConfigurationRequestDto, MetricsConfigurationTableEntity>()
                .ForMember(dest => dest.PartitionKey, opt => opt.MapFrom(src => src.AgentId))
                .ForMember(dest => dest.AgentId, opt => opt.MapFrom(src => src.AgentId)) // Set manually to ensure consistency
                .ForMember(dest => dest.ConfigurationName, opt => opt.MapFrom(src => src.ConfigurationName)) // Set manually to ensure consistency
                .ForMember(dest => dest.EnvironmentName, opt => opt.MapFrom(src => src.EnvironmentName)) // Set manually to ensure consistency
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description)) // Set manually to ensure consistency
                .ForMember(dest => dest.ConfigurationId, opt => opt.MapFrom(src => Guid.NewGuid().ToString())) // Set manually to ensure consistency

                .ForMember(dest => dest.CreatedOn, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.LastUpdatedOn, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => "System"))
                .ForMember(dest => dest.LastUpdatedBy, opt => opt.MapFrom(src => "System"))

                .ForMember(dest => dest.ConainerName, opt => opt.Ignore())
                .ForMember(dest => dest.BlobFilePath, opt => opt.Ignore())
                .ForMember(dest => dest.Timestamp, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.ETag, opt => opt.Ignore());

            CreateMap<MetricsConfigurationTableEntity, CreateConfigurationRequestDto>().ReverseMap(); 

            CreateMap<UpdateMetricsConfigurationRequestDto, MetricsConfigurationTableEntity>()
                .ForMember(dest => dest.PartitionKey, opt => opt.MapFrom(src => src.AgentId))
                .ForMember(dest => dest.AgentId, opt => opt.MapFrom(src => src.AgentId)) // Set manually to ensure consistency
                .ForMember(dest => dest.ConfigurationName, opt => opt.MapFrom(src => src.ConfigurationName)) // Set manually to ensure consistency
                .ForMember(dest => dest.EnvironmentName, opt => opt.MapFrom(src => src.EnvironmentName)) // Set manually to ensure consistency
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description)) // Set manually to ensure consistency
                .ForMember(dest => dest.ConfigurationId, opt => opt.MapFrom(src => src.ConfigurationId)) // Set manually to ensure consistency
                .ForMember(dest => dest.CreatedOn, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.LastUpdatedOn, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => "System"))
                .ForMember(dest => dest.LastUpdatedBy, opt => opt.MapFrom(src => "System"))
                .ForMember(dest => dest.ConainerName, opt => opt.Ignore())
                .ForMember(dest => dest.BlobFilePath, opt => opt.Ignore())
                .ForMember(dest => dest.Timestamp, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.ETag, opt => opt.Ignore());

            CreateMap<MetricsConfigurationTableEntity, UpdateMetricsConfigurationRequestDto>().ReverseMap();

            CreateMap<SelectedMetricsConfigurationDto, SelectedMetricsConfiguration>()
                .ReverseMap();

            CreateMap<MetricsConfigurationTableEntity, MetricsConfigurationMetadataDto>()
                .ForMember(dest => dest.ConfigurationName, opt => opt.MapFrom(src => src.ConfigurationName))
                .ForMember(dest => dest.EnvironmentName, opt => opt.MapFrom(src => src.EnvironmentName))
                .ForMember(dest => dest.AgentId, opt => opt.MapFrom(src => src.AgentId))
                .ForMember(dest => dest.ConfigurationId, opt => opt.MapFrom(src => src.ConfigurationId))
                .ForMember(dest => dest.Description, opt => opt.MapFrom(src => src.Description))
                .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
                .ForMember(dest => dest.CreatedOn, opt => opt.MapFrom(src => src.CreatedOn))
                .ForMember(dest => dest.LastUpdatedBy, opt => opt.MapFrom(src => src.LastUpdatedBy))
                .ForMember(dest => dest.LastUpdatedOn, opt => opt.MapFrom(src => src.LastUpdatedOn));

            CreateMap<EvalRunEntity, EvalRunDto>();

            CreateMap<EvalRunEntity, EvalRunDto>().ReverseMap();

        }

        private void ConfigureDataSetMappings()
        {
            // SaveDatasetDto to DataSetTableEntity (for create operations, optimized)
            CreateMap<SaveDatasetDto, DataSetTableEntity>()
                .ForMember(dest => dest.PartitionKey, opt => opt.MapFrom(src => src.AgentId))
                .ForMember(dest => dest.RowKey, opt => opt.Ignore()) // Set manually with GUID
                .ForMember(dest => dest.DatasetId, opt => opt.Ignore()) // Set manually with GUID
                .ForMember(dest => dest.DatasetName, opt => opt.MapFrom(src => src.DatasetName))
                .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => "System"))
                .ForMember(dest => dest.CreatedOn, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.LastUpdatedOn, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.LastUpdatedBy, opt => opt.MapFrom(src => "System"))
                .ForMember(dest => dest.BlobFilePath, opt => opt.Ignore()) // Set in request handler
                .ForMember(dest => dest.ContainerName, opt => opt.Ignore()) // Set in request handler
                .ForMember(dest => dest.Timestamp, opt => opt.Ignore())
                .ForMember(dest => dest.ETag, opt => opt.Ignore());

            // DataSetTableEntity to DatasetMetadataDto (optimized)
            CreateMap<DataSetTableEntity, DatasetMetadataDto>()
                .ForMember(dest => dest.DatasetName, opt => opt.MapFrom(src => src.DatasetName))
                .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
                .ForMember(dest => dest.CreatedOn, opt => opt.MapFrom(src => src.CreatedOn))
                .ForMember(dest => dest.LastUpdatedBy, opt => opt.MapFrom(src => src.LastUpdatedBy))
                .ForMember(dest => dest.LastUpdatedOn, opt => opt.MapFrom(src => src.LastUpdatedOn))
                .ForMember(dest => dest.RecordCount, opt => opt.Ignore()); // Calculated separately if needed
        }

        private void ConfigureCollectionMappings()
        {
            // Optimized collection mappings with better performance
            CreateMap<IList<SelectedMetricsConfigurationDto>, IList<SelectedMetricsConfiguration>>()
                .ConvertUsing<SelectedMetricsConfigurationDtoListConverter>();

            CreateMap<IList<SelectedMetricsConfiguration>, IList<SelectedMetricsConfigurationDto>>()
                .ConvertUsing<SelectedMetricsConfigurationListConverter>();
        }
    }

    /// <summary>
    /// Custom converter for better performance when converting lists
    /// </summary>
    public class SelectedMetricsConfigurationDtoListConverter : ITypeConverter<IList<SelectedMetricsConfigurationDto>, IList<SelectedMetricsConfiguration>>
    {
        public IList<SelectedMetricsConfiguration> Convert(IList<SelectedMetricsConfigurationDto> source,
            IList<SelectedMetricsConfiguration> destination, ResolutionContext context)
        {
            if (source == null || source.Count == 0)
                return new List<SelectedMetricsConfiguration>();

            var result = new List<SelectedMetricsConfiguration>(source.Count);
            foreach (var item in source)
            {
                result.Add(context.Mapper.Map<SelectedMetricsConfiguration>(item));
            }
            return result;
        }
    }

    /// <summary>
    /// Custom converter for better performance when converting lists (reverse)
    /// </summary>
    public class SelectedMetricsConfigurationListConverter : ITypeConverter<IList<SelectedMetricsConfiguration>, IList<SelectedMetricsConfigurationDto>>
    {
        public IList<SelectedMetricsConfigurationDto> Convert(IList<SelectedMetricsConfiguration> source,
            IList<SelectedMetricsConfigurationDto> destination, ResolutionContext context)
        {
            if (source == null || source.Count == 0)
                return new List<SelectedMetricsConfigurationDto>();

            var result = new List<SelectedMetricsConfigurationDto>(source.Count);
            foreach (var item in source)
            {
                result.Add(context.Mapper.Map<SelectedMetricsConfigurationDto>(item));
            }
            return result;
        }
    }
}


