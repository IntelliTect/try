// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import { expect } from "chai";
import { describe } from "mocha";
import { createApiService } from "../src/apiService";

describe("apiService", () => {
    it("adds traceparent header when correlationContext is a trace id", async () => {
        let capturedHeaders: any = null;
        const originalFetch = globalThis.fetch;

        (globalThis as any).fetch = async (_url: string, options: any) => {
            capturedHeaders = options.headers;
            return {
                ok: true,
                json: async () => ({ events: [] })
            };
        };

        try {
            const service = createApiService({
                commandsUrl: new URL("https://example.org/commands"),
                correlationContext: "0123456789abcdef0123456789abcdef",
                onServiceError: () => { /* no-op */ }
            });

            await service([{ toJson: () => ({ commandType: "SubmitCode", command: {} }) } as any]);

            expect(capturedHeaders).to.have.property("traceparent");
            expect(capturedHeaders.traceparent).to.match(/^00-0123456789abcdef0123456789abcdef-[a-f0-9]{16}-01$/);
        } finally {
            (globalThis as any).fetch = originalFetch;
        }
    });

    it("does not add traceparent header when correlationContext is missing", async () => {
        let capturedHeaders: any = null;
        const originalFetch = globalThis.fetch;

        (globalThis as any).fetch = async (_url: string, options: any) => {
            capturedHeaders = options.headers;
            return {
                ok: true,
                json: async () => ({ events: [] })
            };
        };

        try {
            const service = createApiService({
                commandsUrl: new URL("https://example.org/commands"),
                onServiceError: () => { /* no-op */ }
            });

            await service([{ toJson: () => ({ commandType: "SubmitCode", command: {} }) } as any]);

            expect(capturedHeaders).to.not.have.property("traceparent");
        } finally {
            (globalThis as any).fetch = originalFetch;
        }
    });
});
