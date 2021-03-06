// tslint:disable:variable-name
// tslint:disable:max-line-length
import { EntityForSave } from './base/entity-for-save';
import { TenantWorkspace } from '../workspace.service';
import { TranslateService } from '@ngx-translate/core';
import { SettingsForClient } from '../dto/settings-for-client';
import { EntityDescriptor } from './base/metadata';
import { EntityWithKey } from './base/entity-with-key';
import { Visibility } from '../dto/definitions-for-client';

export type ReportOrderDirection = 'asc' | 'desc';
export type ReportType = 'Summary' | 'Details';
export type Aggregation = 'count' | 'sum' | 'avg' | 'max' | 'min';
export type ChartType = 'Card' | 'BarsVertical' | 'BarsVerticalGrouped' | 'BarsVerticalStacked' |
    'BarsVerticalNormalized' | 'BarsHorizontal' | 'BarsHorizontalGrouped' | 'BarsHorizontalStacked' |
    'BarsHorizontalNormalized' | 'Line' | 'Area' | 'AreaStacked' | 'AreaNormalized' | 'Pie' | 'Doughnut' |
    'HeatMap' | 'TreeMap' | 'NumberCards' | 'Gauge' | 'Radar';
export type MainMenuSection = 'Financials' | 'Administration'; // TODO
export type MainMenuIcon = 'clipboard' | 'chart-pie';
export type DefinitionState = 'Draft' | 'Deployed' | 'Archived';
export type Modifier = 'year' | 'quarter' | 'month' | 'dayofyear' | 'day' | 'week' | 'weekday';

export interface ReportDefinitionForSave<
    TParameter = ReportParameterDefinitionForSave,
    TRow = ReportRowDefinitionForSave,
    TColumn = ReportColumnDefinitionForSave,
    TMeasure = ReportMeasureDefinitionForSave,
    TSelect = ReportSelectDefinitionForSave> extends EntityForSave {

    Title?: string;
    Title2?: string;
    Title3?: string;
    Description?: string;
    Description2?: string;
    Description3?: string;
    Type?: ReportType; // summary or details
    Chart?: ChartType;
    DefaultsToChart?: boolean; // ?
    Collection?: string;
    DefinitionId?: string;
    Filter?: string;
    OrderBy?: string;
    Top?: number;
    ShowColumnsTotal?: boolean;
    ShowRowsTotal?: boolean;
    MainMenuSection?: MainMenuSection;
    MainMenuIcon?: MainMenuIcon;
    MainMenuSortKey?: number;
    ShowInMainMenu?: boolean;

    Select?: TSelect[];
    Parameters?: TParameter[];
    Rows?: TRow[];
    Columns?: TColumn[];
    Measures?: TMeasure[];
}

export interface ReportDefinition extends ReportDefinitionForSave<
    ReportParameterDefinition, ReportRowDefinition, ReportColumnDefinition, ReportMeasureDefinition, ReportSelectDefinition> {
    CreatedAt?: string;
    CreatedById?: number | string;
    ModifiedAt?: string;
    ModifiedById?: number | string;
}

export interface ReportParameterDefinitionForSave extends EntityForSave {
    Key?: string; // e.g. 'FromDate'
    Label?: string;
    Label2?: string;
    Label3?: string;
    Visibility?: Visibility;
    Value?: string;
}

// tslint:disable-next-line:no-empty-interface
export interface ReportParameterDefinition extends ReportParameterDefinitionForSave {
    ReportDefinitionId?: string;
}

export interface ReportSelectDefinitionForSave extends EntityForSave {
    Path?: string;
    Label?: string;
    Label2?: string;
    Label3?: string;
}

// tslint:disable-next-line:no-empty-interface
export interface ReportSelectDefinition extends ReportSelectDefinitionForSave {
    ReportDefinitionId?: string;
}

export interface ReportDimensionDefinition extends EntityForSave {
    Path?: string;
    Modifier?: string;
    Label?: string;
    Label2?: string;
    Label3?: string;
    OrderDirection?: ReportOrderDirection;
    AutoExpand?: boolean;
}

// tslint:disable-next-line:no-empty-interface
export interface ReportColumnDefinitionForSave extends ReportDimensionDefinition {
}

// tslint:disable-next-line:no-empty-interface
export interface ReportColumnDefinition extends ReportColumnDefinitionForSave {
    ReportDefinitionId?: string;
}

// tslint:disable-next-line:no-empty-interface
export interface ReportRowDefinitionForSave extends ReportDimensionDefinition {

}

// tslint:disable-next-line:no-empty-interface
export interface ReportRowDefinition extends ReportRowDefinitionForSave {
    ReportDefinitionId?: string;
}

export interface ReportMeasureDefinitionForSave extends EntityForSave {
    Path?: string;
    Label?: string;
    Label2?: string;
    Label3?: string;
    OrderDirection?: ReportOrderDirection;
    Aggregation?: Aggregation;
}

// tslint:disable-next-line:no-empty-interface
export interface ReportMeasureDefinition extends ReportMeasureDefinitionForSave {
    ReportDefinitionId?: string;
}

const _select = ['', '2', '3'].map(pf => 'Title' + pf);
let _settings: SettingsForClient;
let _cache: EntityDescriptor;

export function metadata_ReportDefinition(ws: TenantWorkspace, trx: TranslateService, _: string): EntityDescriptor {
    // Some global values affect the result, we check here if they have changed, otherwise we return the cached result
    if (ws.settings !== _settings) {
        _settings = ws.settings;
        _cache = {
            collection: 'ReportDefinition',
            titleSingular: () => trx.instant('ReportDefinition'),
            titlePlural: () => trx.instant('ReportDefinitions'),
            apiEndpoint: 'report-definitions',
            screenUrl: 'report-definitions',
            select: _select,
            orderby: ws.isSecondaryLanguage ? [_select[1], _select[0]] : ws.isTernaryLanguage ? [_select[2], _select[0]] : [_select[0]],
            format: (item: EntityWithKey) => (ws.getMultilingualValueImmediate(item, _select[0]) || trx.instant('Untitled')),
            properties: {
                Id: { control: 'text', label: () => trx.instant('Id') },
                Title: { control: 'text', label: () => trx.instant('Title') + ws.primaryPostfix },
                Title2: { control: 'text', label: () => trx.instant('Title') + ws.secondaryPostfix },
                Title3: { control: 'text', label: () => trx.instant('Title') + ws.ternaryPostfix },
                Description: { control: 'text', label: () => trx.instant('Description') + ws.primaryPostfix },
                Description2: { control: 'text', label: () => trx.instant('Description') + ws.secondaryPostfix },
                Description3: { control: 'text', label: () => trx.instant('Description') + ws.ternaryPostfix },
                Type: {
                    control: 'choice',
                    label: () => trx.instant('ReportDefinition_Type'),
                    choices: ['Summary', 'Details'],
                    format: (c: string) => trx.instant(`ReportDefinition_Type_${c}`)
                },
                Chart: {
                    control: 'choice',
                    label: () => trx.instant('ReportDefinition_Chart'),
                    choices: [ // Ordered by number of supported dimensions for ease of selection
                        // 0 Dimensions
                        'Card',
                        // 1 Dimension
                        'BarsVertical', 'BarsHorizontal', 'Pie', 'Doughnut', 'TreeMap', 'NumberCards', 'Gauge',
                        // 1 or 2 Dimensions
                        'Line', 'Area',
                        // 2 Dimensions
                        'BarsVerticalGrouped', 'BarsVerticalStacked', 'BarsVerticalNormalized', 'BarsHorizontalGrouped',
                        'BarsHorizontalStacked', 'BarsHorizontalNormalized', /* 'AreaStacked', 'AreaNormalized', 'Radar', */ 'HeatMap'],
                    format: (c: string) => trx.instant(`ReportDefinition_Chart_${c}`)
                },
                DefaultsToChart: { control: 'boolean', label: () => trx.instant('ReportDefinition_DefaultsToChart') },
                Collection: { control: 'text', label: () => trx.instant('ReportDefinition_Collection') },
                DefinitionId: { control: 'text', label: () => trx.instant('ReportDefinition_DefinitionId') },
                Filter: { control: 'text', label: () => trx.instant('ReportDefinition_Filter') },
                OrderBy: { control: 'text', label: () => trx.instant('ReportDefinition_OrderBy') },
                Top: { control: 'number', label: () => trx.instant('ReportDefinition_Top'), minDecimalPlaces: 0, maxDecimalPlaces: 0 },
                ShowColumnsTotal: { control: 'boolean', label: () => trx.instant('ReportDefinition_ShowColumnsTotal') },
                ShowRowsTotal: { control: 'boolean', label: () => trx.instant('ReportDefinition_ShowRowsTotal') },
                MainMenuSection: {
                    control: 'choice',
                    label: () => trx.instant('MainMenuSection'),
                    choices: ['Financials', 'Administration'],
                    format: (c: string) => trx.instant(c)
                },
                MainMenuIcon: {
                    control: 'choice',
                    label: () => trx.instant('MainMenuIcon'),
                    choices: ['clipboard', 'chart-pie'],
                    format: (c: string) => {
                        switch (c) {
                            case 'clipboard': return trx.instant('Icon_Clipboard');
                            case 'chart-pie': return trx.instant('Icon_ChartPie');
                            default: return c;
                        }
                    },
                },
                MainMenuSortKey: { control: 'number', label: () => trx.instant('MainMenuSortKey'), minDecimalPlaces: 2, maxDecimalPlaces: 2 },
                ShowInMainMenu: { control: 'boolean', label: () => trx.instant('ReportDefinition_ShowInMainMenu') },
                CreatedAt: { control: 'datetime', label: () => trx.instant('CreatedAt') },
                CreatedBy: { control: 'navigation', label: () => trx.instant('CreatedBy'), type: 'User', foreignKeyName: 'CreatedById' },
                ModifiedAt: { control: 'datetime', label: () => trx.instant('ModifiedAt') },
                ModifiedBy: { control: 'navigation', label: () => trx.instant('ModifiedBy'), type: 'User', foreignKeyName: 'ModifiedById' }
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
