// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import * as polyglotNotebooks from '@microsoft/polyglot-notebooks';

export interface IServiceError {
    statusCode: string;
    message: string;
    requestId?: string;
};

export interface IApiServiceConfiguration {
    referer?: URL;
    commandsUrl: URL;
    correlationContext?: string;
    onServiceError: (error: IServiceError) => void;
}


export function createApiService(configuration: IApiServiceConfiguration): IApiService {
    return createApiServiceWithConfiguration(configuration);
}
export interface IApiService {
    (commands: polyglotNotebooks.KernelCommandEnvelope[]): Promise<polyglotNotebooks.KernelEventEnvelope[]>
}


function createApiServiceWithConfiguration(configuration: IApiServiceConfiguration): IApiService {
    const traceId = normalizeTraceId(configuration.correlationContext);
    let service: IApiService = async (commands) => {
        let bodyContent = JSON.stringify({
            commands: commands.map(command => command.toJson())
        });
        let headers = {
            'Content-Type': 'application/json'
        };
        if (configuration.referer) {
            headers['Referer'] = configuration.referer.toString();
        }
        if (traceId) {
            headers['traceparent'] = createTraceParent(traceId);
        }

        let response = await fetch(configuration.commandsUrl.toString(), {
            method: 'POST',
            headers: headers,
            body: bodyContent
        });

        polyglotNotebooks.Logger.default.info(`[ApiService.request] ${bodyContent}`);

        if (!response.ok) {
            configuration.onServiceError({
                statusCode: `${response.status}`,
                message: response.statusText
            });
            throw new Error(`${response.status} ${response.statusText}`);
        }

        let json = await response.json();

        polyglotNotebooks.Logger.default.info(`[ApiService.response] ${JSON.stringify(json)}`);

        const srcEvents = json.events as polyglotNotebooks.KernelEventEnvelopeModel[];
        return srcEvents.map(srcEvent => polyglotNotebooks.KernelEventEnvelope.fromJson(srcEvent));
    };

    return service;
}

function normalizeTraceId(value?: string): string | null {
    if (!value) {
        return null;
    }

    const traceParentMatch = value.match(/^00-([a-fA-F0-9]{32})-[a-fA-F0-9]{16}-[a-fA-F0-9]{2}$/);
    if (traceParentMatch) {
        return traceParentMatch[1].toLowerCase();
    }

    if (/^[a-fA-F0-9]{32}$/.test(value)) {
        return value.toLowerCase();
    }

    return null;
}

function createTraceParent(traceId: string): string {
    return `00-${traceId}-${createSpanId()}-01`;
}

function createSpanId(): string {
    const randomValues = new Uint8Array(8);
    crypto.getRandomValues(randomValues);
    return Array.from(randomValues, b => b.toString(16).padStart(2, '0')).join('');
}
