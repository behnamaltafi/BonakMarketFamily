using BonakMarket.Scheduling;

namespace BonakMarket.Core.Data;

public partial class SmartDbContext
{
    public DbSet<TaskDescriptor> TaskDescriptors { get; set; }
    public DbSet<TaskExecutionInfo> TaskExecutionInfos { get; set; }
}