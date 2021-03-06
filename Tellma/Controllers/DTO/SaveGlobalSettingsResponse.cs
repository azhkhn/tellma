﻿namespace Tellma.Controllers.Dto
{
    public class SaveGlobalSettingsResponse : GetByIdResponse<GlobalSettings>
    {
        public DataWithVersion<GlobalSettingsForClient> SettingsForClient { get; set; }
    }
}
