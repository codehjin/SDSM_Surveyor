namespace SDSM_Surveyor_App.InjectableServices;

// DI 자동 등록용 마커 인터페이스 (CLAUDE.md §2.3)
public interface IInjectablesService { }
public interface ITransientService : IInjectablesService { }
public interface IScopedService : IInjectablesService { }
public interface ISingletonService : IInjectablesService { }
