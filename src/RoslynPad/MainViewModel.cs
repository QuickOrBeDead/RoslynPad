namespace RoslynPad
{
    using System;
    using System.Collections.Immutable;
    using System.Composition;
    using System.Reflection;

    using RoslynPad.UI;

    [Export(typeof(MainViewModelBase)), Shared]
    public class MainViewModel : MainViewModelBase
    {
        [ImportingConstructor]
        public MainViewModel(IServiceProvider serviceProvider, IExceptionManager exceptionManager, ICommandProvider commands, IApplicationSettings settings, NuGetViewModel nugetViewModel, DocumentFileWatcher documentFileWatcher) : 
            base(serviceProvider, exceptionManager, commands, settings, nugetViewModel, documentFileWatcher)
        {
        }

        protected override ImmutableArray<Assembly> CompositionAssemblies => base.CompositionAssemblies
            .Add(Assembly.Load(new AssemblyName("RoslynPad.Roslyn.Windows")))
            .Add(Assembly.Load(new AssemblyName("RoslynPad.Editor")));
    }
}
