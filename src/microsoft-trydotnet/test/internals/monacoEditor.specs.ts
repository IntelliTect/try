// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


import { expect } from "chai";
import { MonacoTextEditor } from "../../src/internals/monacoTextEditor";
import { FakeMessageBus } from "../fakes/fakeMessageBus";
import { FakeIdGenerator } from "../fakes/fakeIdGenerator";
import * as newContract from "../../src/newContract";


describe("a monaco editor", () => {

    let bus: FakeMessageBus;
    let idGenerator: FakeIdGenerator;
    let editor: MonacoTextEditor;

    beforeEach(() => {
        bus = new FakeMessageBus("test bus");
        idGenerator = new FakeIdGenerator();
        editor = new MonacoTextEditor(bus, idGenerator);
    });

    it("can set the theme as string", () => {
        let messages: { type: string, requestId?: string }[] = [];
        bus.requests.subscribe({ next: m => messages.push(m) });
        editor.setTheme("different theme");
        expect(messages).to.not.be.empty;
        expect(messages[0].type).to.equal(newContract.ConfigureMonacoEditorType);
    });

    it("can set the theme as object", () => {
        let messages: { type: string, requestId?: string }[] = [];
        bus.requests.subscribe({ next: m => messages.push(m) });
        editor.setTheme({
            name: "different theme",
            monacoEditorTheme: {
                base: 'vs-dark',
                inherit: true,
                rules: [{
                    token: 'comment',
                    foreground: 'red',
                    fontStyle: 'italic'
                }]
            }
        });
        expect(messages).to.not.be.empty;
        expect(messages[0].type).to.equal(newContract.DefineMonacoEditorThemesType);
        expect((messages[0] as any).themes).to.deep.equal({
            "different theme": {
                base: 'vs-dark',
                inherit: true,
                rules: [{
                    token: 'comment',
                    foreground: 'red',
                    fontStyle: 'italic'
                }]
            }
        });
        expect(messages[1].type).to.equal(newContract.ConfigureMonacoEditorType);
    });

    it("can set the editor options", () => {
        let messages: { type: string, requestId?: string }[] = [];
        bus.requests.subscribe({ next: m => messages.push(m) });
        editor.setOptions({
            minimap: {
                enabled: false
            }
        });
        expect(messages).to.not.be.empty;
        expect(messages[0].type).to.equal(newContract.ConfigureMonacoEditorType);
        expect(messages[0].type).to.equal(newContract.ConfigureMonacoEditorType);
        expect((messages[0] as any).editorOptions.minimap).to.deep.equal(
            {
                enabled: false
            });
    });

    it("can be configured", () => {
        let messages: { type: string, requestId?: string }[] = [];
        bus.requests.subscribe({ next: m => messages.push(m) });
        editor.configure({
            theme: "different theme",
            options: {
                minimap: {
                    enabled: false
                }
            }
        });
        expect(messages).to.not.be.empty;
        expect(messages[0].type).to.equal(newContract.ConfigureMonacoEditorType);
        expect((messages[0] as any).editorOptions.minimap).to.deep.equal(
            {
                enabled: false
            });
        expect(messages[1].type).to.equal(newContract.ConfigureMonacoEditorType);
        expect((messages[1] as any).theme).to.equal("different theme");
    });

});