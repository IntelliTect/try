// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import { Workspace } from "../../src/internals/workspace";
import { createProject } from "../../src";
import { FakeMonacoTextEditor } from "../fakes/fakeMonacoTextEditor";
import { FakeMessageBus } from "../fakes/fakeMessageBus";
import { expect } from "chai";
import { RequestIdGenerator } from "../../src/internals/requestIdGenerator";
import * as polyglotNotebooks from "@microsoft/polyglot-notebooks";
import * as newContract from "../../src/newContract";

describe("a workspace", () => {

    it("is not marked as modified at creation", () => {
        let ws = new Workspace(new FakeMessageBus("0"), new RequestIdGenerator());
        expect(ws.isModified()).to.be.false;
    });

    it("is not marked as modified when propulated from a project", async () => {
        const bus = new FakeMessageBus("0");

        bus.requests.subscribe({
            next: (message) => {
                message;//?
                switch (message.type) {
                    case polyglotNotebooks.OpenProjectType: {
                        const res: newContract.ProjectOpened = {
                            requestId: message.requestId!,
                            type: polyglotNotebooks.ProjectOpenedType,
                            editorId: bus.id(),
                            projectItems: [{
                                relativeFilePath: "program.cs",
                                regionNames: [],
                                regionsContent: {}
                            },
                            {
                                relativeFilePath: "otherFile.cs",
                                regionNames: [],
                                regionsContent: {}
                            }]
                        }
                        bus.postResponse(res);
                    }
                        break;
                }
            }
        });

        let ws = new Workspace(bus, new RequestIdGenerator());

        let project = await createProject({ packageName: "console", files: [{ name: "program.cs", content: "the program" }, { name: "otherFile.cs", content: "other file content" }] });
        await ws.fromProject(project);
        expect(ws.isModified()).to.be.false;
    });



    it("is is not marked as modified when a document is opened", async () => {
        const bus = new FakeMessageBus("0");

        bus.requests.subscribe({
            next: (message) => {
                message;//?
                switch (message.type) {
                    case polyglotNotebooks.OpenProjectType: {
                        const res: newContract.ProjectOpened = {
                            requestId: message.requestId!,
                            type: polyglotNotebooks.ProjectOpenedType,
                            editorId: bus.id(),
                            projectItems: [{
                                relativeFilePath: "program.cs",
                                regionNames: [],
                                regionsContent: {}
                            },
                            {
                                relativeFilePath: "otherFile.cs",
                                regionNames: [],
                                regionsContent: {}
                            }]
                        }
                        bus.postResponse(res);
                    }
                        break;
                    case polyglotNotebooks.OpenDocumentType: {
                        const res: newContract.DocumentOpened = {
                            requestId: message.requestId!,
                            type: polyglotNotebooks.DocumentOpenedType,
                            editorId: bus.id(),
                            content: "the program",
                            relativeFilePath: "program.cs"
                        };
                        bus.postResponse(res);
                    }
                        break;
                }
            }
        });

        let ws = new Workspace(bus, new RequestIdGenerator());
        let project = await createProject({ packageName: "console", files: [{ name: "program.cs", content: "the program" }, { name: "otherFile.cs", content: "other file content" }] });
        await ws.fromProject(project);
        await ws.openDocument({ fileName: "program.cs" });
        expect(ws.isModified()).to.be.false;
    });

    it("is not marked as modified when a document is opened again", async () => {
        const bus = new FakeMessageBus("0");

        bus.requests.subscribe({
            next: (message) => {
                message;//?
                switch (message.type) {
                    case polyglotNotebooks.OpenProjectType: {
                        const res: newContract.ProjectOpened = {
                            requestId: message.requestId!,
                            type: polyglotNotebooks.ProjectOpenedType,
                            editorId: bus.id(),
                            projectItems: [{
                                relativeFilePath: "program.cs",
                                regionNames: [],
                                regionsContent: {}
                            },
                            {
                                relativeFilePath: "otherFile.cs",
                                regionNames: [],
                                regionsContent: {}
                            }]
                        }
                        bus.postResponse(res);
                    }
                        break;
                    case polyglotNotebooks.OpenDocumentType: {
                        const res: newContract.DocumentOpened = {
                            requestId: message.requestId!,
                            type: polyglotNotebooks.DocumentOpenedType,
                            editorId: bus.id(),
                            content: "the program",
                            relativeFilePath: "program.cs"
                        };
                        bus.postResponse(res);
                    }
                        break;
                }
            }
        });

        let ws = new Workspace(bus, new RequestIdGenerator());
        let project = await createProject({ packageName: "console", files: [{ name: "program.cs", content: "the program" }, { name: "otherFile.cs", content: "other file content" }] });
        await ws.fromProject(project);
        let doc = await ws.openDocument({ fileName: "program.cs" });
        ws.toOpenProjectRequests();
        expect(ws.isModified()).to.be.false;
        doc = await ws.openDocument({ fileName: "program.cs" });
    });

    it("is marked as modified when a document is opened and  the content is changed", async () => {
        const bus = new FakeMessageBus("0");

        bus.requests.subscribe({
            next: (message) => {
                message;//?
                switch (message.type) {
                    case polyglotNotebooks.OpenProjectType: {
                        const res: newContract.ProjectOpened = {
                            requestId: message.requestId!,
                            type: polyglotNotebooks.ProjectOpenedType,
                            editorId: bus.id(),
                            projectItems: [{
                                relativeFilePath: "program.cs",
                                regionNames: [],
                                regionsContent: {}
                            },
                            {
                                relativeFilePath: "otherFile.cs",
                                regionNames: [],
                                regionsContent: {}
                            }]
                        }
                        bus.postResponse(res);
                    }
                        break;

                    case polyglotNotebooks.OpenDocumentType: {
                        const res: newContract.DocumentOpened = {
                            requestId: message.requestId!,
                            type: polyglotNotebooks.DocumentOpenedType,
                            editorId: bus.id(),
                            content: "the program",
                            relativeFilePath: "program.cs"
                        };
                        bus.postResponse(res);
                    }
                        break;
                }
            }
        });

        let ws = new Workspace(bus, new RequestIdGenerator());
        let project = await createProject({ packageName: "console", files: [{ name: "program.cs", content: "the program" }, { name: "otherFile.cs", content: "other file content" }] });
        await ws.fromProject(project);
        let doc = await ws.openDocument({ fileName: "program.cs" });
        doc.setContent("modified content");
        expect(ws.isModified()).to.be.true;
    });

    it("generates openProject request object", async () => {
        const bus = new FakeMessageBus("0");

        bus.requests.subscribe({
            next: (message) => {
                message;//?
                switch (message.type) {
                    case polyglotNotebooks.OpenProjectType: {
                        const res: newContract.ProjectOpened = {
                            requestId: message.requestId!,
                            type: polyglotNotebooks.ProjectOpenedType,
                            editorId: bus.id(),
                            projectItems: [{
                                relativeFilePath: "program.cs",
                                regionNames: [],
                                regionsContent: {}
                            },
                            {
                                relativeFilePath: "otherFile.cs",
                                regionNames: [],
                                regionsContent: {}
                            }]
                        }
                        bus.postResponse(res);
                    }
                        break;
                    case polyglotNotebooks.OpenDocumentType: {
                        const res: newContract.DocumentOpened = {
                            requestId: message.requestId!,
                            type: polyglotNotebooks.DocumentOpenedType,
                            editorId: bus.id(),
                            content: "the program",
                            relativeFilePath: "program.cs"
                        };
                        bus.postResponse(res);
                    }
                        break;
                }
            }
        });

        let ws = new Workspace(bus, new RequestIdGenerator());
        let project = await createProject({ packageName: "console", files: [{ name: "program.cs", content: "the program" }, { name: "otherFile.cs", content: "other file content" }] });
        await ws.fromProject(project);
        let doc = await ws.openDocument({ fileName: "program.cs" });
        doc.setContent("modified content");
        let opr = ws.toOpenProjectRequests();
        expect(opr).not.to.be.null;
        expect(opr.project).not.to.be.null;
        expect(opr.project.files[0]).not.to.be.null;
    });

    it("can open a document and set its content", async () => {
        const bus = new FakeMessageBus("0");

        bus.requests.subscribe({
            next: (message) => {
                message;//?
                switch (message.type) {
                    case polyglotNotebooks.OpenProjectType: {
                        const res: newContract.ProjectOpened = {
                            requestId: message.requestId!,
                            type: polyglotNotebooks.ProjectOpenedType,
                            editorId: bus.id(),
                            projectItems: [{
                                relativeFilePath: "program.cs",
                                regionNames: [],
                                regionsContent: {}
                            },
                            {
                                relativeFilePath: "otherFile.cs",
                                regionNames: [],
                                regionsContent: {}
                            }]
                        }
                        bus.postResponse(res);
                    }
                        break;
                    case polyglotNotebooks.OpenDocumentType: {
                        const res: newContract.DocumentOpened = {
                            requestId: message.requestId!,
                            type: polyglotNotebooks.DocumentOpenedType,
                            editorId: bus.id(),
                            content: "the program",
                            relativeFilePath: "program.cs"
                        };
                        bus.postResponse(res);
                    }
                        break;
                }
            }
        });

        let ws = new Workspace(bus, new RequestIdGenerator());
        let project = await createProject(
            {
                packageName: "console",
                files: [
                    { name: "program.cs", content: "the program" },
                    { name: "otherFile.cs", content: "other file content" }
                ]
            });

        await ws.fromProject(project);
        let doc = await ws.openDocument({ fileName: "program.cs", content: "content override" });
        expect(doc).not.to.be.null;
        expect(doc.getContent()).to.equal("content override");
    });

    it("can open a document in the editor and set its content", async () => {
        const bus = new FakeMessageBus("0");

        bus.requests.subscribe({
            next: (message) => {
                message;//?
                switch (message.type) {
                    case polyglotNotebooks.OpenProjectType: {
                        const res: newContract.ProjectOpened = {
                            requestId: message.requestId!,
                            type: polyglotNotebooks.ProjectOpenedType,
                            editorId: bus.id(),
                            projectItems: [{
                                relativeFilePath: "program.cs",
                                regionNames: [],
                                regionsContent: {}
                            },
                            {
                                relativeFilePath: "otherFile.cs",
                                regionNames: [],
                                regionsContent: {}
                            }]
                        }
                        bus.postResponse(res);
                    }
                        break;
                    case polyglotNotebooks.OpenDocumentType: {
                        const res: newContract.DocumentOpened = {
                            requestId: message.requestId!,
                            type: polyglotNotebooks.DocumentOpenedType,
                            editorId: bus.id(),
                            content: "the program",
                            relativeFilePath: "program.cs"
                        };
                        bus.postResponse(res);
                    }
                        break;
                }
            }
        });

        let ws = new Workspace(bus, new RequestIdGenerator());
        let project = await createProject(
            {
                packageName: "console",
                files: [
                    { name: "program.cs", content: "the program" },
                    { name: "otherFile.cs", content: "other file content" }
                ]
            });

        let editorZero = new FakeMonacoTextEditor("0");
        await ws.fromProject(project);
        let doc = await ws.openDocument({ fileName: "program.cs", textEditor: editorZero, content: "content override" });
        expect(doc).not.to.be.null;
        expect(doc.getContent()).to.equal("content override");
        expect(doc.currentEditor()).not.to.be.null;
        expect(doc.currentEditor().id()).to.equal("0");
        expect(editorZero.content).to.equal(doc.getContent());
    });
});
