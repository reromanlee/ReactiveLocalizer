using reromanlee.ReactiveLocalizer.Samples.QuickStart;
using reromanlee.ReactiveLocalizer.Unity;
using VContainer;
using VContainer.Unity;

namespace reromanlee.ReactiveLocalizer.Samples.VContainer
{
    /// <summary>
    /// Registers the localizer of one catalog with VContainer, as <see cref="ILocalizer"/> and <see cref="Localizer"/>,
    /// and initializes it asynchronously as the container starts:
    /// <code>
    /// protected override void Configure(IContainerBuilder builder)
    /// {
    ///     new LocalizerInstaller(LocalizationKeys.CatalogKey).Install(builder);
    /// }
    /// </code>
    /// </summary>
    /// <remarks>
    /// The container creates and disposes the localizer. It is also handed to Quick Start's
    /// <see cref="GlobalLocalizer"/> as the container builds, so components built on it, such as the TextMeshPro
    /// sample's, show text from this instance, and it starts in the player's saved language.
    /// </remarks>
    public sealed class LocalizerInstaller : IInstaller
    {
        private readonly CatalogKey _catalog;

        /// <param name="catalog">The catalog to localize.</param>
        public LocalizerInstaller(CatalogKey catalog)
        {
            _catalog = catalog;
        }

        public void Install(IContainerBuilder builder)
        {
            CatalogKey catalog = _catalog;
            builder.Register(_ => new Localizer(catalog, new UnityHost()), Lifetime.Singleton).As<ILocalizer>().AsSelf();
            builder.RegisterEntryPoint<LocalizerStartup>();
            // Handed over as soon as the container is built, before any component asks the access point for one.
            builder.RegisterBuildCallback(container => GlobalLocalizer.Use(catalog, container.Resolve<ILocalizer>()));
        }
    }
}
