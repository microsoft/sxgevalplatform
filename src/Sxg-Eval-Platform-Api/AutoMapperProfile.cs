using AutoMapper;
using Sxg.EvalPlatform.API.Storage.Entities;
using Sxg.EvalPlatform.API.Storage.TableEntities;
using SXG.EvalPlatform.Common;
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
            // CreateMetricsConfigurationDto to MetricsConfigurationTableEntity
            CreateMap<CreateMetricsConfigurationDto, MetricsConfigurationTableEntity>()
                //.ForMember(dest => dest.PartitionKey, opt => opt.MapFrom(src => src.AgentId))
                .ForMember(dest => dest.AgentId, opt => opt.MapFrom(src => src.AgentId))
                .ForMember(dest => dest.ConfigurationId, opt => opt.MapFrom(src => src.ConfigurationId ?? Guid.NewGuid().ToString()))
                .ForMember(dest => dest.LastUpdatedOn, opt => opt.MapFrom(src => DateTime.UtcNow))
                .ForMember(dest => dest.ConainerName, opt => opt.MapFrom(src => CommonUtils.TrimAndRemoveSpaces(src.AgentId)))
                .ForMember(dest => dest.BlobFilePath, opt => opt.Ignore())
                .ForMember(dest => dest.Timestamp, opt => opt.Ignore())
                .ForMember(dest => dest.ETag, opt => opt.Ignore());

            // MetricsConfigurationTableEntity to CreateMetricsConfigurationDto
            CreateMap<MetricsConfigurationTableEntity, CreateMetricsConfigurationDto>()
                .ForMember(dest => dest.MetricsConfiguration, opt => opt.Ignore()); // This is handled separately via blob storage

            // SelectedMetricsConfigurationDto to SelectedMetricsConfiguration
            CreateMap<SelectedMetricsConfigurationDto, SelectedMetricsConfiguration>()
                .ReverseMap();

            // MetricsConfigurationTableEntity to MetricsConfigurationMetadataDto
            CreateMap<MetricsConfigurationTableEntity, MetricsConfigurationMetadataDto>()
                .ForMember(dest => dest.MetricsConfiguration, opt => opt.Ignore()); // This is loaded from blob storage separately

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


