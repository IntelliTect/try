// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

import { expect } from "chai";
import { DocumentId } from "../../src/documentId";
import { Document } from "../../src/internals/document";
import { FakeMonacoTextEditor } from "../fakes/fakeMonacoTextEditor";


describe("a document", () => {

    it("is not marked as modified at creation", () => {
        let document = new Document(DocumentId.parse("program.cs"), "content");
        expect(document.isModified).to.be.false;
    });

    it("is not marked as modified when the content is changed from editor", async () => {
        let document = new Document(DocumentId.parse("program.cs"), "content");
        let editor = new FakeMonacoTextEditor("0");
        await document.bindToEditor(editor);
        editor.raiseTextEvent("other content");
        expect(document.isModified).to.be.false;
        expect(document.getContent()).to.equal("other content");
    });

    it("is active if bound to an editor", async () => {
        let document = new Document(DocumentId.parse("program.cs"), "content");
        let editor = new FakeMonacoTextEditor("0");
        expect(document.isActiveInEditor()).to.be.false;
        await document.bindToEditor(editor);
        expect(document.isActiveInEditor()).to.be.true;
    });

    it("is marked as modified when the content is changed via setContent", async () => {
        let document = new Document(DocumentId.parse("program.cs"), "content");
        await document.setContent("modified content");
        expect(document.isModified).to.be.true;
    });

});