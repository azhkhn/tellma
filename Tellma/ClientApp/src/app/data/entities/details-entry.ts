
// tslint:disable:variable-name
// tslint:disable:max-line-length
import { TenantWorkspace } from '../workspace.service';
import { TranslateService } from '@ngx-translate/core';
import { EntityDescriptor } from './base/metadata';
import { SettingsForClient } from '../dto/settings-for-client';
import { EntityWithKey } from './base/entity-with-key';

export interface DetailsEntry extends EntityWithKey {
    LineId?: number;
    ResponsibilityCenterId?: number;
    Direction?: number;
    AccountId?: number;
    AgentId: number;
    EntryTypeId?: number;
    ResourceId?: number;
    DueDate?: string;
    MonetaryValue?: number;
    CurrencyId?: number;
    Count?: number;
    NormalizedCount?: number;
    Mass?: number;
    NormalizedMass?: number;
    Volume?: number;
    NormalizedVolume?: number;
    Time?: number;
    Value?: number;
    ExternalReference?: string;
    AdditionalReference?: string;
    NotedAgentId?: number;
    NotedAgentName?: string;
    NotedAmount?: number;
    NotedDate?: string;
}

let _settings: SettingsForClient;
let _cache: EntityDescriptor;

export function metadata_DetailsEntry(ws: TenantWorkspace, trx: TranslateService, _: string): EntityDescriptor {
    // Some global values affect the result, we check here if they have changed, otherwise we return the cached result
    if (ws.settings !== _settings) {
        _settings = ws.settings;
        const entityDesc: EntityDescriptor = {
            collection: 'DetailsEntry',
            titleSingular: () => trx.instant('DetailsEntry'),
            titlePlural: () => trx.instant('DetailsEntries'),
            select: [],
            apiEndpoint: 'details-entries',
            parameters: [
                // TODO
                { key: 'CountUnitId', isRequired: false, desc: { control: 'navigation', label: () => trx.instant('Resource_CountUnit'), type: 'MeasurementUnit', foreignKeyName: 'CountUnitId' } },
                { key: 'MassUnitId', isRequired: false, desc: { control: 'navigation', label: () => trx.instant('Resource_MassUnit'), type: 'MeasurementUnit', foreignKeyName: 'CountUnitId' } },
                { key: 'VolumeUnitId', isRequired: false, desc: { control: 'navigation', label: () => trx.instant('Resource_VolumeUnit'), type: 'MeasurementUnit', foreignKeyName: 'CountUnitId' } },
            ],
            screenUrl: 'details-entries', // TODO
            orderby: ['Id'],
            format: (item: EntityWithKey) => '',
            properties: {
                Id: { control: 'number', label: () => trx.instant('Id'), minDecimalPlaces: 0, maxDecimalPlaces: 0 },
                LineId: { control: 'number', label: () => `${trx.instant('Entry_Line')} (${trx.instant('Id')})`, minDecimalPlaces: 0, maxDecimalPlaces: 0 },
                Line: { control: 'navigation', label: () => trx.instant('Entry_Line'), type: 'Line', foreignKeyName: 'LineId' },
                ResponsibilityCenterId: { control: 'number', label: () => `${trx.instant('Entry_ResponsibilityCenter')} (${trx.instant('Id')})`, minDecimalPlaces: 0, maxDecimalPlaces: 0 },
                ResponsibilityCenter: { control: 'navigation', label: () => trx.instant('Entry_ResponsibilityCenter'), type: 'ResponsibilityCenter', foreignKeyName: 'ResponsibilityCenterId' },
                Direction: {
                    control: 'choice',
                    label: () => trx.instant('Entry_Direction'),
                    choices: [-1, 1],
                    format: (c: number) => {
                        switch (c) {
                            case 1: return trx.instant('Entry_Direction_Debit');
                            case -1: return trx.instant('Entry_Direction_Credit');
                            default: return '';
                        }
                    }
                },
                AccountId: { control: 'number', label: () => `${trx.instant('Entry_Account')} (${trx.instant('Id')})`, minDecimalPlaces: 0, maxDecimalPlaces: 0 },
                Account: { control: 'navigation', label: () => trx.instant('Entry_Account'), type: 'Account', foreignKeyName: 'AccountId' },
                AgentId: { control: 'number', label: () => `${trx.instant('Entry_Agent')} (${trx.instant('Id')})`, minDecimalPlaces: 0, maxDecimalPlaces: 0 },
                Agent: { control: 'navigation', label: () => trx.instant('Entry_Agent'), type: 'Agent', foreignKeyName: 'AgentId' },
                EntryTypeId: { control: 'number', label: () => `${trx.instant('Entry_EntryType')} (${trx.instant('Id')})`, minDecimalPlaces: 0, maxDecimalPlaces: 0 },
                EntryType: { control: 'navigation', label: () => trx.instant('Entry_EntryType'), type: 'EntryType', foreignKeyName: 'EntryTypeId' },
                ResourceId: { control: 'number', label: () => `${trx.instant('Entry_Resource')} (${trx.instant('Id')})`, minDecimalPlaces: 0, maxDecimalPlaces: 0 },
                Resource: { control: 'navigation', label: () => trx.instant('Entry_Resource'), type: 'Resource', foreignKeyName: 'ResourceId' },
                DueDate: { control: 'date', label: () => trx.instant('Entry_DueDate') },
                MonetaryValue: { control: 'number', label: () => trx.instant('Entry_MonetaryValue'), minDecimalPlaces: 0, maxDecimalPlaces: 4 },
                CurrencyId: { control: 'text', label: () => `${trx.instant('Entry_Currency')} (${trx.instant('Id')})` },
                Currency: { control: 'navigation', label: () => trx.instant('Entry_Currency'), type: 'Currency', foreignKeyName: 'CurrencyId' },
                Count: { control: 'number', label: () => trx.instant('Entry_Count'), minDecimalPlaces: 0, maxDecimalPlaces: 4 },
                NormalizedCount: { control: 'number', label: () => trx.instant('DetailsEntry_NormalizedCount'), minDecimalPlaces: 0, maxDecimalPlaces: 4 },
                Mass: { control: 'number', label: () => trx.instant('Entry_Mass'), minDecimalPlaces: 0, maxDecimalPlaces: 4 },
                NormalizedMass: { control: 'number', label: () => trx.instant('DetailsEntry_NormalizedMass'), minDecimalPlaces: 0, maxDecimalPlaces: 4 },
                Volume: { control: 'number', label: () => trx.instant('Entry_Volume'), minDecimalPlaces: 0, maxDecimalPlaces: 4 },
                NormalizedVolume: { control: 'number', label: () => trx.instant('DetailsEntry_NormalizedVolume'), minDecimalPlaces: 0, maxDecimalPlaces: 4 },
                Time: { control: 'number', label: () => trx.instant('Entry_Time'), minDecimalPlaces: 0, maxDecimalPlaces: 4 },
                Value: { control: 'number', label: () => trx.instant('Entry_Value'), minDecimalPlaces: ws.settings.FunctionalCurrencyDecimals, maxDecimalPlaces: ws.settings.FunctionalCurrencyDecimals, alignment: 'right' },
                ExternalReference: { control: 'text', label: () => trx.instant('Entry_ExternalReference') },
                AdditionalReference: { control: 'text', label: () => trx.instant('Entry_AdditionalReference') },
                NotedAgentId: { control: 'number', label: () => `${trx.instant('Entry_NotedAgent')} (${trx.instant('Id')})`, minDecimalPlaces: 0, maxDecimalPlaces: 0 },
                NotedAgent: { control: 'navigation', label: () => trx.instant('Entry_NotedAgent'), type: 'Agent', foreignKeyName: 'AgentId' },
                NotedAgentName: { control: 'text', label: () => trx.instant('Entry_NotedAgentName') },
                NotedAmount: { control: 'number', label: () => trx.instant('Entry_NotedAmount'), minDecimalPlaces: 0, maxDecimalPlaces: 4 },
                NotedDate: { control: 'date', label: () => trx.instant('Entry_NotedDate') },
            }
        };

        _cache = entityDesc;
    }

    return _cache;
}
