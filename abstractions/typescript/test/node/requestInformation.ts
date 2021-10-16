/**
 * -------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation.  All Rights Reserved.  Licensed under the MIT License.
 * See License in the project root for license information.
 * -------------------------------------------------------------------------------------------
 */

import { assert } from "chai";
import { Readable } from "stream";

import { RequestInformation } from "../../src/";

describe("RequestInformation", () => {
	it("Should set request information uri", () => {
		const requestInformation = new RequestInformation();
		const nodeReadableStream = new Readable();
		requestInformation.setStreamContent(nodeReadableStream);

		assert.equal(requestInformation["content"], nodeReadableStream);
	});
});