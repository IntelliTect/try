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

    it("uses an updated correlationContext for requests after the service is created", async () => {
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
            service.setCorrelationContext("fedcba9876543210fedcba9876543210");

            await service([{ toJson: () => ({ commandType: "SubmitCode", command: {} }) } as any]);

            expect(capturedHeaders.traceparent).to.match(/^00-fedcba9876543210fedcba9876543210-[a-f0-9]{16}-01$/);
        } finally {
            (globalThis as any).fetch = originalFetch;
        }
    });

    it("ignores non-string correlation contexts and allows null or undefined to clear", async () => {
        const capturedHeaders: any[] = [];
        const originalFetch = globalThis.fetch;

        (globalThis as any).fetch = async (_url: string, options: any) => {
            capturedHeaders.push(options.headers);
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
            const commands = [{ toJson: () => ({ commandType: "SubmitCode", command: {} }) } as any];

            service.setCorrelationContext?.({} as any);
            await service(commands);
            service.setCorrelationContext?.("fedcba9876543210fedcba9876543210");
            service.setCorrelationContext?.(null);
            await service(commands);
            service.setCorrelationContext?.("fedcba9876543210fedcba9876543210");
            service.setCorrelationContext?.(undefined);
            await service(commands);

            expect(capturedHeaders[0].traceparent).to.match(/^00-0123456789abcdef0123456789abcdef-[a-f0-9]{16}-01$/);
            expect(capturedHeaders[1]).to.not.have.property("traceparent");
            expect(capturedHeaders[2]).to.not.have.property("traceparent");
        } finally {
            (globalThis as any).fetch = originalFetch;
        }
    });
});
