using System;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using BonakMarket.Engine;
using BonakMarket.Engine.Modularity;
using BonakMarket.Test.Common;

namespace BonakMarket.Tests;

public abstract class TypeScannerBase : TestsBase
{
    protected ITypeScanner typeScanner;

    protected abstract Type[] GetTypes();

    [OneTimeSetUp]
    public override void SetUp()
    {
        base.SetUp();
        typeScanner = new DefaultTypeScanner(
            new[] { typeof(TypeScannerBase).Assembly },
            new ModuleCatalog(Array.Empty<IModuleDescriptor>()),
            NullLogger.Instance);
    }
}