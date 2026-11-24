using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using BonakMarket.Data.Hooks;
using BonakMarket.Domain;
using EntityState = BonakMarket.Data.EntityState;

namespace BonakMarket.Tests.Data.Hooks;

internal class HookedEntityMock : IHookedEntity
{
    private BaseEntity _entity;
    private EntityState _state;

    public HookedEntityMock(BaseEntity entity, EntityState state, DbContext dbContext)
    {
        _entity = entity;
        _state = state;
        DbContext = dbContext;
        InitialState = state;
    }

    public DbContext DbContext { get; set; }

    public EntityEntry Entry => null;

    public BaseEntity Entity => _entity;

    public Type EntityType => _entity.GetType();

    public EntityState InitialState
    {
        get;
        set;
    }

    public EntityState State
    {
        get => _state;
        set => _state = value;
    }

    public bool HasStateChanged => InitialState != _state;

    public bool? IsSoftDeleted { get; set; }

    public bool IsPropertyModified(string propertyName) => false;
}