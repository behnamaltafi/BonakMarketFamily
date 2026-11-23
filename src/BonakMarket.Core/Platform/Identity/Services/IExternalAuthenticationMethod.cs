using BonakMarket.Core.Widgets;
using BonakMarket.Engine.Modularity;

namespace BonakMarket.Core.Identity;

public partial interface IExternalAuthenticationMethod : IProvider, IUserEditable
{
    /// <summary>
    /// Gets an invoker for displaying a widget.
    /// </summary>
    /// <param name="storeId">The id of the current store.</param>
    Widget GetDisplayWidget(int storeId);
}