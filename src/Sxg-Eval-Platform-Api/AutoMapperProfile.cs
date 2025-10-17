using AutoMapper;
using Sxg.EvalPlatform.API.Storage.Entities;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SxgEvalPlatformApi.Models;
using SxgEvalPlatformApi.Models.Dtos;

namespace SxgEvalPlatformApi
{

    /// <summary>
    /// AutoMapper profile for mapping DTOs to entities and vice versa
    /// </summary>
    public class MappingProfile : Profile
    {

        


        public MappingProfile()
        {
            // CreateConfigurationRequestDto to MetricsConfigurationTableEntity  
            CreateMap<CreateConfigurationRequestDto, MetricsConfigurationTableEntity>()
                .ForMember(dest => dest.PartitionKey, opt => opt.MapFrom(src => src.AgentId))
                .ForMember(dest => dest.RowKey, opt => opt.MapFrom(src => Guid.NewGuid().ToString()))
                .ForMember(dest => dest.ConfigurationId, opt => opt.Ignore()) // Set manually to ensure consistency
                .ForMember(dest => dest.CreatedOn, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.LastUpdatedOn, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.UserMetadata.Email))
                .ForMember(dest => dest.LastUpdatedBy, opt => opt.MapFrom(src => src.UserMetadata.Email))
                .ForMember(dest => dest.ConainerName, opt => opt.MapFrom(src => src.AgentId))
                .ForMember(dest => dest.BlobFilePath, opt => opt.Ignore())
                .ForMember(dest => dest.Timestamp, opt => opt.Ignore())
                .ForMember(dest => dest.ETag, opt => opt.Ignore());

            // UpdateConfigurationRequestDto to MetricsConfigurationTableEntity  
            CreateMap<UpdateConfigurationRequestDto, MetricsConfigurationTableEntity>()
                .ForMember(dest => dest.PartitionKey, opt => opt.Ignore()) // Preserve existing
                .ForMember(dest => dest.RowKey, opt => opt.Ignore()) // Preserve existing
                .ForMember(dest => dest.ConfigurationId, opt => opt.Ignore()) // Preserve existing
                .ForMember(dest => dest.CreatedOn, opt => opt.Ignore()) // Preserve existing
                .ForMember(dest => dest.CreatedBy, opt => opt.Ignore()) // Preserve existing
                .ForMember(dest => dest.LastUpdatedOn, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.LastUpdatedBy, opt => opt.MapFrom(src => src.UserMetadata.Email))
                .ForMember(dest => dest.ConainerName, opt => opt.Ignore()) // Preserve existing
                .ForMember(dest => dest.BlobFilePath, opt => opt.Ignore()) // Preserve existing
                .ForMember(dest => dest.Timestamp, opt => opt.Ignore())
                .ForMember(dest => dest.ETag, opt => opt.Ignore());

            // SelectedMetricsConfigurationDto to SelectedMetricsConfiguration
            CreateMap<SelectedMetricsConfigurationDto, SelectedMetricsConfiguration>()
                .ReverseMap();

            // MetricsConfigurationTableEntity to MetricsConfigurationMetadataDto
            CreateMap<MetricsConfigurationTableEntity, MetricsConfigurationMetadataDto>()
                .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
                .ForMember(dest => dest.CreatedOn, opt => opt.MapFrom(src => src.CreatedOn))
                .ForMember(dest => dest.LastUpdatedBy, opt => opt.MapFrom(src => src.LastUpdatedBy))
                .ForMember(dest => dest.LastUpdatedOn, opt => opt.MapFrom(src => src.LastUpdatedOn));

            // SaveDatasetDto to DataSetTableEntity (for create operations)
            CreateMap<SaveDatasetDto, DataSetTableEntity>()
                .ForMember(dest => dest.PartitionKey, opt => opt.MapFrom(src => src.AgentId))
                .ForMember(dest => dest.RowKey, opt => opt.Ignore()) // Set manually with GUID
                .ForMember(dest => dest.DatasetId, opt => opt.Ignore()) // Set manually with GUID
                .ForMember(dest => dest.DatasetName, opt => opt.MapFrom(src => src.DatasetName))
                .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.UserMetadata.Email))
                .ForMember(dest => dest.CreatedOn, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.LastUpdatedOn, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.LastUpdatedBy, opt => opt.MapFrom(src => src.UserMetadata.Email))
                .ForMember(dest => dest.BlobFilePath, opt => opt.Ignore()) // Set in request handler
                .ForMember(dest => dest.ContainerName, opt => opt.Ignore()) // Set in request handler
                .ForMember(dest => dest.Timestamp, opt => opt.Ignore())
                .ForMember(dest => dest.ETag, opt => opt.Ignore());

            // DataSetTableEntity to DatasetMetadataDto
            CreateMap<DataSetTableEntity, DatasetMetadataDto>()
                .ForMember(dest => dest.DatasetName, opt => opt.MapFrom(src => src.DatasetName))
                .ForMember(dest => dest.CreatedBy, opt => opt.MapFrom(src => src.CreatedBy))
                .ForMember(dest => dest.CreatedOn, opt => opt.MapFrom(src => src.CreatedOn))
                .ForMember(dest => dest.LastUpdatedBy, opt => opt.MapFrom(src => src.LastUpdatedBy))
                .ForMember(dest => dest.LastUpdatedOn, opt => opt.MapFrom(src => src.LastUpdatedOn))
                .ForMember(dest => dest.RecordCount, opt => opt.Ignore()); // Calculated separately if needed

            // Additional mappings for collections
            CreateMap<IList<SelectedMetricsConfigurationDto>, IList<SelectedMetricsConfiguration>>()
                .ConvertUsing((src, dest, context) =>
                    src?.Select(x => context.Mapper.Map<SelectedMetricsConfiguration>(x)).ToList() ?? new List<SelectedMetricsConfiguration>());

            CreateMap<IList<SelectedMetricsConfiguration>, IList<SelectedMetricsConfigurationDto>>()
                .ConvertUsing((src, dest, context) =>
                    src?.Select(x => context.Mapper.Map<SelectedMetricsConfigurationDto>(x)).ToList() ?? new List<SelectedMetricsConfigurationDto>());
        }
    }



}


