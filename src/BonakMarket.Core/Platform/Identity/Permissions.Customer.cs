namespace BonakMarket.Core.Security;

public static partial class Permissions
{
    public static class Customer
    {
        public const string Self = "customer";
        public const string Read = "customer.read";
        public const string Update = "customer.update";
        public const string Create = "customer.create";
        public const string Delete = "customer.delete";
        public const string Impersonate = "customer.impersonate";
        public const string ReadAddress = "customer.readaddress";
        public const string CreateAddress = "customer.createaddress";
        public const string EditAddress = "customer.editaddress";
        public const string DeleteAddress = "customer.deleteaddress";
        public const string EditRole = "customer.editcustomerrole";
        public const string SendPm = "customer.sendpm";

        public static class Guild
        {
            public const string Self = "customer.guild";
            public const string Read = "customer.guild.read";
            public const string Create = "customer.guild.create";
            public const string Update = "customer.guild.update";
            public const string Delete = "customer.guild.delete";
        }

        public static class Role
        {
            public const string Self = "customer.role";
            public const string Read = "customer.role.read";
            public const string Update = "customer.role.update";
            public const string Create = "customer.role.create";
            public const string Delete = "customer.role.delete";
        }
    }
}