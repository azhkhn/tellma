import { Component, Input } from '@angular/core';
import { tap } from 'rxjs/operators';
import { ApiService } from '~/app/data/api.service';
import { Role, RoleForSave } from '~/app/data/entities/role';
import { Permission } from '~/app/data/entities/permission';
import { addToWorkspace } from '~/app/data/util';
import { WorkspaceService } from '~/app/data/workspace.service';
import { DetailsBaseComponent } from '~/app/shared/details-base/details-base.component';
import { TranslateService } from '@ngx-translate/core';
import { DefinitionsForClient } from '~/app/data/dto/definitions-for-client';
import { VIEWS_BUILT_IN, ACTIONS } from '~/app/data/views';
import { metadata_Lookup } from '~/app/data/entities/lookup';
import { metadata_Resource } from '~/app/data/entities/resource';
import { metadata_LegacyType } from '~/app/data/entities/legacy-type';
import { metadata_Account } from '~/app/data/entities/account';
import { SelectorChoice } from '~/app/shared/selector/selector.component';
import { metadata_Agent } from '~/app/data/entities/agent';
import { metadata_Document } from '~/app/data/entities/document';

interface ConcreteViewInfo {
  name: () => string;
  actions: { [action: string]: { supportsCriteria: boolean, supportsMask: boolean } };
}

@Component({
  selector: 't-roles-details',
  templateUrl: './roles-details.component.html'
})
export class RolesDetailsComponent extends DetailsBaseComponent {

  @Input()
  public showMembers = true;

  @Input()
  public showPermissions = true;

  @Input()
  public showIsPublic = true;

  private _permissionActionChoices: { [view: string]: SelectorChoice[] } = {};
  private _viewsDb: { [view: string]: ConcreteViewInfo } = null;
  private _currentLang: string;
  private _currentDefinition: DefinitionsForClient;
  private _currentViewsDb: { [view: string]: ConcreteViewInfo } = null;
  private _viewsForSelector: SelectorChoice[] = null;
  private rolesApi = this.api.rolesApi(this.notifyDestruct$); // for intellisense

  public expand = 'Permissions,Members/User';

  create = () => {
    const result: RoleForSave = { };
    if (this.ws.isPrimaryLanguage) {
      result.Name = this.initialText;
    } else if (this.ws.isSecondaryLanguage) {
      result.Name2 = this.initialText;
    } else if (this.ws.isTernaryLanguage) {
      result.Name3 = this.initialText;
    }
    result.IsPublic = false;
    result.Permissions = [];
    result.Members = [];
    return result;
  }

  clone: (item: Role) => Role = (item: Role) => {
    if (!!item) {
      const clone = JSON.parse(JSON.stringify(item)) as Role;
      clone.Id = null;

      if (!!clone.Permissions) {
        clone.Permissions.forEach(e => {
          e.Id = null;
          delete e.RoleId;
        });
      }
      if (!!clone.Members) {
        clone.Members.forEach(e => {
          e.Id = null;
          delete e.RoleId;
        });
      }

      return clone;
    } else {
      // programmer mistake
      console.error('Cloning a non existing item');
      return null;
    }
  }

  constructor(public workspace: WorkspaceService, private api: ApiService, private translate: TranslateService) {
    super();

    this.rolesApi = this.api.rolesApi(this.notifyDestruct$);
  }

  permissionActionChoices(item: Permission): SelectorChoice[] {
    if (!item.View) {
      return [];
    }

    // Returns the permission actions only permitted by the specified view
    if (!this._permissionActionChoices[item.View]) {
      const view = this.viewsDb[item.View];
      if (!!view && !!view.actions) {
        this._permissionActionChoices[item.View] =
          Object.keys(view.actions).map(e => ({ name: () => this.translate.instant(ACTIONS[e]), value: e }));
      } else {
        this._permissionActionChoices[item.View] = [];
      }
    }

    return this._permissionActionChoices[item.View];
  }

  public permissionActionLookup(value: string): string {
    if (!value) {
      return '';
    }

    if (value === 'All') {
      return 'View_All';
    }

    return ACTIONS[value];
  }

  public disableCriteria(item: Permission): boolean {
    if (!item || !item.View || !item.Action) {
      return true;
    }
    const view = this.viewsDb[item.View];
    if (!!view && !!view.actions) {
      const viewAction = view.actions[item.Action];
      return !(viewAction && viewAction.supportsCriteria);
    } else {
      return true;
    }
  }

  public disableMask(item: Permission): boolean {
    if (!item || !item.View || !item.Action) {
      return true;
    }
    const view = this.viewsDb[item.View];
    if (!!view && !!view.actions) {
      const viewAction = view.actions[item.Action];
      return !(viewAction && viewAction.supportsMask);
    } else {
      return true;
    }
  }

  public onPermissionChanged(item: Permission) {
    // Here we clear away fields that are not compatible with other field values
    const choices = this.permissionActionChoices(item);
    if (choices.length === 1) {
      item.Action = choices[0].value;
    } else if (!!item.Action && choices.every(e => e.value !== item.Action)) {
      item.Action = null;
    }

    if (this.disableMask(item)) {
      item.Mask = null;
    }

    if (this.disableCriteria(item)) {
      item.Criteria = null;
    }
  }

  public onActivate = (model: Role): void => {
    if (!!model && !!model.Id) {
      this.rolesApi.activate([model.Id], { returnEntities: true, expand: this.expand }).pipe(
        tap(res => addToWorkspace(res, this.workspace))
      ).subscribe({ error: this.details.handleActionError });
    }
  }

  public onDeactivate = (model: Role): void => {
    if (!!model && !!model.Id) {
      this.rolesApi.deactivate([model.Id], { returnEntities: true, expand: this.expand }).pipe(
        tap(res => addToWorkspace(res, this.workspace))
      ).subscribe({ error: this.details.handleActionError });
    }
  }

  public showActivate = (model: Role) => !!model && !model.IsActive;
  public showDeactivate = (model: Role) => !!model && model.IsActive;

  public canActivateDeactivateItem = (model: Role) => this.ws.canDo('roles', 'IsActive', model.Id);

  public activateDeactivateTooltip = (model: Role) => this.canActivateDeactivateItem(model) ? '' :
    this.translate.instant('Error_AccountDoesNotHaveSufficientPermissions')

  public get ws() {
    return this.workspace.current;
  }

  permissionsCount(model: Role): number | string {
    return !!model && !!model.Permissions ? model.Permissions.length : 0;
  }

  membersCount(model: Role): number | string {
    return !!model && !!model.Members ? model.Members.length : 0;
  }

  showMembersTab(model: Role) {
    return this.showMembers && (!model || !model.IsPublic);
  }

  showPublicRoleWarning(model: Role) {
    return !model || model.IsPublic;
  }

  showPermissionsError(model: Role) {
    return !!model && !!model.Permissions && model.Permissions.some(e => !!e.serverErrors);
  }

  showMembersError(model: Role) {
    return !!model && !!model.Members && model.Members.some(e => !!e.serverErrors);
  }

  // viewFormatter: (id: number | string) => string = (id: number | string) =>
  //   !!this.ws.get('View', id) && !!this.ws.get('View', id).ResourceName ?
  //     (this.translate.instant(this.ws.get('View', id).ResourceName)) :
  //     this.ws.getMultilingualValue('View', id, 'Name')

  get viewsDb(): { [view: string]: ConcreteViewInfo } {

    if (this._currentLang !== this.translate.currentLang || this._currentDefinition !== this.ws.definitions) {
      this._currentLang = this.translate.currentLang;
      this._currentDefinition = this.ws.definitions;

      this._viewsDb = {};
      for (const view of Object.keys(VIEWS_BUILT_IN)) {
        const viewInfo = VIEWS_BUILT_IN[view];
        const concreteView: ConcreteViewInfo = {
          name: () => this.translate.instant(viewInfo.name),
          actions: {}
        };

        concreteView.actions.All = { supportsCriteria: false, supportsMask: false };

        if (viewInfo.read) {
          concreteView.actions.Read = { supportsCriteria: true, supportsMask: true };
        }

        if (viewInfo.update) {
          concreteView.actions.Update = { supportsCriteria: true, supportsMask: true };
        }

        if (viewInfo.delete) {
          concreteView.actions.Delete = { supportsCriteria: true, supportsMask: false };
        }

        for (const action of viewInfo.actions) {
          concreteView.actions[action.action] = { supportsCriteria: action.criteria, supportsMask: false };
        }

        this._viewsDb[view] = concreteView;
      }

      const lookups = this.ws.definitions.Lookups;
      for (const definitionId of Object.keys(lookups)) {
        const entityDesc = metadata_Lookup(this.ws, this.translate, definitionId);
        if (!!entityDesc) {
          this._viewsDb[entityDesc.apiEndpoint] = {
            name: entityDesc.titlePlural,
            actions: {
              All: { supportsCriteria: false, supportsMask: false },
              Read: { supportsCriteria: true, supportsMask: true },
              Update: { supportsCriteria: true, supportsMask: true },
              Delete: { supportsCriteria: true, supportsMask: false },
              IsActive: { supportsCriteria: true, supportsMask: false },
            }
          };
        } else {
          console.error(`Could not find lookup definitionId '${definitionId}'`);
        }
      }

      const agents = this.ws.definitions.Agents;
      for (const definitionId of Object.keys(agents)) {
        const entityDesc = metadata_Agent(this.ws, this.translate, definitionId);
        if (!!entityDesc) {
          this._viewsDb[entityDesc.apiEndpoint] = {
            name: entityDesc.titlePlural,
            actions: {
              All: { supportsCriteria: false, supportsMask: false },
              Read: { supportsCriteria: true, supportsMask: true },
              Update: { supportsCriteria: true, supportsMask: true },
              Delete: { supportsCriteria: true, supportsMask: false },
              IsActive: { supportsCriteria: true, supportsMask: false },
            }
          };
        } else {
          console.error(`Could not find agent definitionId '${definitionId}'`);
        }
      }

      const resources = this.ws.definitions.Resources;
      for (const definitionId of Object.keys(resources)) {
        const entityDesc = metadata_Resource(this.ws, this.translate, definitionId);
        if (!!entityDesc) {
          this._viewsDb[entityDesc.apiEndpoint] = {
            name: entityDesc.titlePlural,
            actions: {
              All: { supportsCriteria: false, supportsMask: false },
              Read: { supportsCriteria: true, supportsMask: true },
              Update: { supportsCriteria: true, supportsMask: true },
              Delete: { supportsCriteria: true, supportsMask: false },
              IsActive: { supportsCriteria: true, supportsMask: false },
            }
          };
        } else {
          console.error(`Could not find resource definitionId '${definitionId}'`);
        }
      }

      const documents = this.ws.definitions.Documents;
      for (const definitionId of Object.keys(documents)) {
        const entityDesc = metadata_Document(this.ws, this.translate, definitionId);
        if (!!entityDesc) {
          this._viewsDb[entityDesc.apiEndpoint] = {
            name: entityDesc.titlePlural,
            actions: {
              All: { supportsCriteria: false, supportsMask: false },
              Read: { supportsCriteria: true, supportsMask: true },
              Update: { supportsCriteria: true, supportsMask: true },
              Delete: { supportsCriteria: true, supportsMask: false },
            }
          };
        }
      }
    }

    return this._viewsDb;
  }

  get permissionViewChoices(): SelectorChoice[] {

    if (this._currentViewsDb !== this.viewsDb) {
      const db = this.viewsDb;
      this._currentViewsDb = db;

      this._viewsForSelector = Object.keys(db).map(e => ({ value: e, name: db[e].name }));

      // Sort alphabetically
      this._viewsForSelector.sort((a, b) => a.name() < b.name() ? -1 : a.name() > b.name() ? 1 : 0);
    }

    return this._viewsForSelector;
  }

  permissionViewLookup(view: string): string {
    return this.viewsDb[view].name();
  }
}
