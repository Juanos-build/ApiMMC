using AutoMapper;

namespace ApiMMC.Models.Entities
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Energy, EnergyInternal>();
            CreateMap<EnergyConfig, EnergyXmInternal>()
                .ForMember(dest =>
                   dest.KTE,
                   opt => opt.MapFrom(src => src.KTE.ToString()))
                .ForMember(dest =>
                   dest.EnergyReadding,
                   opt => opt.MapFrom(src => src.EnergyReadding.ToString()));
        }
    }

    public static class MapperBootstrapper
    {
        private static IMapper _instance;
        public static IMapper Instance => _instance;

        public static void Configure()
        {
            if (_instance == null)
            {
                var config = new MapperConfiguration(cfg =>
                {
                    cfg.AddProfile<MappingProfile>();
                });
                _instance = config.CreateMapper();
            }
        }
    }
}
