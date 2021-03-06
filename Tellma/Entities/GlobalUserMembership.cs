﻿namespace Tellma.Entities
{
    public class GlobalUserMembershipForSave : EntityWithKey<int>
    {
        public int? UserId { get; set; }

        public int? DatabaseId { get; set; }
    }

    public class GlobalUserMembership : GlobalUserMembershipForSave
    {
        public AdminUser User { get; set; }

        public SqlDatabase Database { get; set; }
    }
}
