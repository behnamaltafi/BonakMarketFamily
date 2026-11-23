using BonakMarket.Engine.Modularity;

namespace BonakMarket.Events;

public class EventConsumerMetadata
{
    public Type ContainerType { get; set; }
    public IModuleDescriptor ModuleDescriptor { get; set; }
}