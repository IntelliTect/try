// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import * as tryDotNetEditor from './tryDotNetEditor';
import * as messages from './legacyTryDotNetMessages';
import * as factory from './factory';
import './index.css';
import * as rxjs from 'rxjs';
import * as monacoAdapterImpl from './monacoAdapterImpl';
import * as apiService from './apiService';
import * as polyglotNotebooks from '@microsoft/polyglot-notebooks';
import { configureLogging } from './log';

const sdkUrl = "https://js.monitor.azure.com/scripts/b/ai.3.gbl.min.js";

if (window) {
	const settings: TryDotNetEditorSettings = {
		editorId: "-0-"
	};

	const configuration: factory.IConfiguration = JSON.parse(document.getElementById("trydotnet-editor-script").dataset.trydotnetConfiguration);

	console.log(`[trydotnet-editor] configuration: ${JSON.stringify(configuration)}`);

	configureLogging({ enableLogging: configuration.enableLogging });
    void initializeAppInsights(configuration);

	const frame = window?.frameElement as HTMLIFrameElement;
	if (frame) {

		let editorId = frame?.dataset["trydotnetEditorId"];
		if (editorId) {
			settings.editorId = editorId.toString();
		}
	}

	let messageDestination = "";
	let mainWindowOrParent: Window = window;
	if (window.parent) {
		mainWindowOrParent = window.parent;
		polyglotNotebooks.Logger.default.info("editor in iframe setup");
		messageDestination = `" to hosting window ${document.referrer}`;
	}

	const postAndLog = (message: any) => {

		message.editorId = settings.editorId;
		polyglotNotebooks.Logger.default.info(`[sending from trydotnet-editor${messageDestination}] ${JSON.stringify(message)}`);
		const messageLogger = window['postMessageLogger'];
		if (messageLogger) {
			messageLogger(message);
		}
		mainWindowOrParent.postMessage(message, '*');
	};

	const mainWindowMessages = new rxjs.Subject<any>();
	window.addEventListener('message', (message) => {
		polyglotNotebooks.Logger.default.info(`[received in trydotnet-editor] ${JSON.stringify(message)}`);
		const apiMessage = message.data;
		if (apiMessage) {
			mainWindowMessages.next(apiMessage);
		}
	}, false);

	const container = configuration.editorContainer ? document.getElementById(configuration.editorContainer) : document.body;
	const editor = factory.createEditor(container ?? document.body);
	const kernel = factory.createWasmProjectKernel((serviceError: apiService.IServiceError) => {
		postAndLog({
			type: messages.SERVICE_ERROR_RESPONSE,
			serviceError: serviceError
		});
	});
	const tdnEditor = new tryDotNetEditor.TryDotNetEditor(message => postAndLog(message), mainWindowMessages, kernel);

	tdnEditor.editor = new monacoAdapterImpl.MonacoEditorAdapter(editor);
	tdnEditor.editorId = settings.editorId;
    document.body.classList.add('monaco-editor-background');

	window['trydotnetEditor'] = tdnEditor;

	// for messaging api backward compatibility
	postAndLog({
		type: messages.HOST_EDITOR_READY_EVENT
	});
	postAndLog({
		type: messages.HOST_RUN_READY_EVENT
	});
}

async function initializeAppInsights(configuration: factory.IConfiguration): Promise<void> {
    const connectionString = configuration.applicationInsightsConnectionString?.trim();
    const traceId = normalizeTraceId(configuration.correlationContext);

    if (!connectionString || !traceId) {
        return;
    }

    await ensureAppInsightsSdkLoaded();

    const ApplicationInsights = getApplicationInsightsConstructor();
    if (!ApplicationInsights) {
        return;
    }

    const appInsights = new ApplicationInsights({
        config: {
            connectionString,
            disableAjaxTracking: false,
            disableFetchTracking: false,
            distributedTracingMode: 2,
            enableCorsCorrelation: true
        }
    });

    appInsights.addTelemetryInitializer((item) => {
        item.tags = item.tags || [];

        if (item.ext?.trace) {
            item.ext.trace.traceID = traceId;
            item.ext.trace.parentID = item.ext.trace.parentID || createSpanId();
        } else {
            item.ext = item.ext || {};
            item.ext.trace = {
                traceID: traceId,
                parentID: createSpanId()
            };
        }
    });

    appInsights.loadAppInsights();
    appInsights.trackPageView({ name: "TryDotNet Editor" });
    instrumentRunRequests(appInsights, traceId);
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

function createSpanId(): string {
    const randomValues = new Uint8Array(8);
    crypto.getRandomValues(randomValues);
    return Array.from(randomValues, b => b.toString(16).padStart(2, '0')).join('');
}

function ensureAppInsightsSdkLoaded(): Promise<void> {
    if (getApplicationInsightsConstructor()) {
        return Promise.resolve();
    }

    return new Promise((resolve, reject) => {
        const existing = document.querySelector(`script[src="${sdkUrl}"]`) as HTMLScriptElement | null;
        if (existing) {
            existing.addEventListener('load', () => resolve(), { once: true });
            existing.addEventListener('error', () => reject(new Error('Failed to load App Insights SDK.')), { once: true });
            return;
        }

        const script = document.createElement('script');
        script.src = sdkUrl;
        script.async = true;
        script.defer = true;
        script.addEventListener('load', () => resolve(), { once: true });
        script.addEventListener('error', () => reject(new Error('Failed to load App Insights SDK.')), { once: true });
        document.head.appendChild(script);
    });
}

function getApplicationInsightsConstructor():
    | (new (options: { config: Record<string, unknown> }) => {
        addTelemetryInitializer: (initializer: (item: any) => void) => void;
        loadAppInsights: () => void;
        trackPageView: (pageView: { name: string }) => void;
        trackDependencyData: (dependency: {
            id: string;
            absoluteUrl: string;
            method: string;
            responseCode: number;
            success: boolean;
            duration: number;
        }) => void;
    })
    | null {
    const candidate = (window as typeof window & {
        Microsoft?: {
            ApplicationInsights?: {
                ApplicationInsights?: new (options: { config: Record<string, unknown> }) => any;
            };
        };
    }).Microsoft?.ApplicationInsights?.ApplicationInsights;

    return candidate ?? null;
}

function instrumentRunRequests(appInsights: any, traceId: string): void {
    const originalFetch = window.fetch.bind(window);

    window.fetch = async (input: RequestInfo | URL, init?: RequestInit) => {
        const url = typeof input === 'string'
            ? input
            : input instanceof URL
                ? input.toString()
                : input.url;
        const method = (init?.method || (typeof input !== 'string' && !(input instanceof URL) ? input.method : 'GET') || 'GET').toUpperCase();

        if (!url.includes('/commands')) {
            return originalFetch(input as any, init);
        }

        const dependencyId = `|${traceId}.${createSpanId()}.`;
        const startTime = performance.now();

        try {
            const response = await originalFetch(input as any, init);

            appInsights.trackDependencyData({
                id: dependencyId,
                absoluteUrl: url,
                method,
                responseCode: response.status,
                success: response.ok,
                duration: performance.now() - startTime
            });

            return response;
        } catch (error) {
            appInsights.trackDependencyData({
                id: dependencyId,
                absoluteUrl: url,
                method,
                responseCode: 0,
                success: false,
                duration: performance.now() - startTime
            });

            throw error;
        }
    };
}

interface TryDotNetEditorSettings {
	editorId: string;
};
