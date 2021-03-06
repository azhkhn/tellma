// tslint:disable:variable-name
import { EntityForSave } from './base/entity-for-save';
import { Permission, PermissionForSave } from './permission';
import { RoleMembershipForSave, RoleMembership } from './role-membership';
import { EntityDescriptor } from './base/metadata';
import { TenantWorkspace } from '../workspace.service';
import { TranslateService } from '@ngx-translate/core';
import { EntityWithKey } from './base/entity-with-key';
import { SettingsForClient } from '../dto/settings-for-client';

export interface RoleForSave<TPermission = PermissionForSave,
    TRoleMembership = RoleMembershipForSave> extends EntityForSave {
    Name?: string;
    Name2?: string;
    Name3?: string;
    Code?: string;
    IsPublic?: boolean;
    Permissions?: TPermission[];
    Members?: TRoleMembership[];
}

export interface Role extends RoleForSave<Permission, RoleMembership> {
    IsActive?: boolean;
    CreatedAt?: string;
    CreatedById?: number | string;
    ModifiedAt?: string;
    ModifiedById?: number | string;
}

const _select = ['', '2', '3'].map(pf => 'Name' + pf);
let _settings: SettingsForClient;
let _cache: EntityDescriptor;

export function metadata_Role(ws: TenantWorkspace, trx: TranslateService, _: string): EntityDescriptor {
  // Some global values affect the result, we check here if they have changed, otherwise we return the cached result
  if (ws.settings !== _settings) {
    _settings = ws.settings;
    _cache = {
      collection: 'Role',
      titleSingular: () => trx.instant('Role'),
      titlePlural:  () => trx.instant('Roles'),
      select: _select,
      apiEndpoint: 'roles',
      screenUrl: 'roles',
      orderby: ws.isSecondaryLanguage ? [_select[1], _select[0]] : ws.isTernaryLanguage ? [_select[2], _select[0]] : [_select[0]],
      format: (item: EntityWithKey) => ws.getMultilingualValueImmediate(item, _select[0]),
      properties: {
        Id: { control: 'number', label: () => trx.instant('Id'), minDecimalPlaces: 0, maxDecimalPlaces: 0 },
        Name: { control: 'text', label: () => trx.instant('Name') + ws.primaryPostfix },
        Name2: { control: 'text', label: () => trx.instant('Name') + ws.secondaryPostfix },
        Name3: { control: 'text', label: () => trx.instant('Name') + ws.ternaryPostfix },
        Code: { control: 'text', label: () => trx.instant('Code') },
        IsPublic: { control: 'boolean', label: () => trx.instant('Role_IsPublic') },
        IsActive: { control: 'boolean', label: () => trx.instant('IsActive') },
        // CreatedAt: { control: 'datetime', label: () => trx.instant('CreatedAt') },
        // CreatedBy: { control: 'navigation', label: () => trx.instant('CreatedBy'), type: 'User', foreignKeyName: 'CreatedById' },
        // ModifiedAt: { control: 'datetime', label: () => trx.instant('ModifiedAt') },
        // ModifiedBy: { control: 'navigation', label: () => trx.instant('ModifiedBy'), type: 'User', foreignKeyName: 'ModifiedById' }
        SavedBy: { control: 'navigation', label: () => trx.instant('ModifiedBy'), type: 'User', foreignKeyName: 'SavedById' }
      }
    };

    if (!ws.settings.SecondaryLanguageId) {
      delete _cache.properties.Name2;
    }

    if (!ws.settings.TernaryLanguageId) {
      delete _cache.properties.Name3;
    }
  }

  return _cache;
}
