// Avalonia.Browser host of the Blazor implementations of Essentieables.
//
// Blazor ships a JS interop dispatcher in blazor.webassembly.js / blazor.server.js; an Avalonia.Browser app
// has none. This module attaches the same dispatcher (@microsoft/dotnet-js-interop, see dotnet-js-interop.js)
// to AvaloniaBrowserJSRuntime, so BlazorEssentials.js and the Blazor/*.cs implementations run unchanged:
// IJSObjectReference, IJSStreamReference, DotNetObjectReference callbacks and byte arrays all work as in Blazor.
//
// Imported by AvaloniaBrowserJSRuntime.AttachAsync() with JSHost.ImportAsync; copied to wwwroot/ on build.
import { DotNet } from './dotnet-js-interop.js';

let dispatcher = null;
let post = null;

function toUint8(data) {
	if (!data)
		return new Uint8Array(0);
	return data instanceof Uint8Array ? data : new Uint8Array(data);
}

function bytesToBase64(bytes) {
	let binary = "";
	const chunk = 0x8000;
	for (let i = 0; i < bytes.length; i += chunk)
		binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunk));
	return btoa(binary);
}

/**
 * Connects the dispatcher to .NET. onMessage receives every call from JS to .NET as a JSON array:
 *   ["b", callId, assemblyName, methodIdentifier, dotNetObjectId, argsJson]  invokeMethodAsync
 *   ["e", serializedResult]                                                    end of a .NET -> JS call
 *   ["y", id, base64]                                                          byte array to revive
 */
export function attach(onMessage) {
	post = onMessage;
	if (dispatcher)
		return;

	dispatcher = DotNet.attachDispatcher({
		beginInvokeDotNetFromJS(callId, assemblyName, methodIdentifier, dotNetObjectId, argsJson) {
			post(JSON.stringify(["b", callId, assemblyName ?? null, methodIdentifier, dotNetObjectId ?? null, argsJson ?? "[]"]));
		},
		endInvokeJSFromDotNet(asyncHandle, succeeded, serializedArgs) {
			post(JSON.stringify(["e", serializedArgs]));
		},
		sendByteArray(id, data) {
			post(JSON.stringify(["y", id, bytesToBase64(toUint8(data))]));
		},
	});

	// The global a Blazor page has: lets page scripts create stream/object references for .NET.
	if (!globalThis.DotNet)
		globalThis.DotNet = DotNet;
}

export function beginInvokeJS(asyncHandle, identifier, argsJson, resultType, targetInstanceId, callType) {
	// Never rejects: failures are reported to .NET through endInvokeJSFromDotNet.
	dispatcher.beginInvokeJSFromDotNet(asyncHandle, identifier, argsJson, resultType, targetInstanceId, callType);
}

export function endInvokeDotNet(callId, success, resultOrError) {
	dispatcher.endInvokeDotNetFromJS(String(callId), success, resultOrError);
}

export function receiveByteArray(id, data) {
	dispatcher.receiveByteArray(id, toUint8(data));
}

/** A DotNetStreamReference sent to JS: its content, or the error that occurred while reading it. */
export function supplyDotNetStream(streamId, data, error) {
	const bytes = toUint8(data);
	const stream = new ReadableStream({
		start(controller) {
			if (error) {
				controller.error(new Error(error));
				return;
			}
			if (bytes.length)
				controller.enqueue(bytes);
			controller.close();
		}
	});
	dispatcher.supplyDotNetStream(streamId, stream);
}

// ---------- IJSStreamReference: read by chunks (Blob or typed array) ----------

const stagedChunks = new Map();
let nextChunkToken = 1;

/** Takes the chunk staged by essentieablesInterop.stageChunk (synchronous [JSImport]). */
export function takeStagedChunk(token) {
	const chunk = stagedChunks.get(token);
	stagedChunks.delete(token);
	return chunk ?? new Uint8Array(0);
}

globalThis.essentieablesInterop = Object.assign(globalThis.essentieablesInterop ?? {}, {
	// Called through the dispatcher, so the IJSStreamReference argument is revived to its Blob / typed array.
	async stageChunk(data, offset, size) {
		let bytes;
		if (data instanceof Blob) {
			bytes = new Uint8Array(await data.slice(offset, offset + size).arrayBuffer());
		} else if (data instanceof ArrayBuffer) {
			bytes = new Uint8Array(data, offset, Math.max(0, Math.min(size, data.byteLength - offset))).slice();
		} else if (data && data.buffer instanceof ArrayBuffer) {
			const length = Math.max(0, Math.min(size, data.byteLength - offset));
			bytes = new Uint8Array(data.buffer, data.byteOffset + offset, length).slice();
		} else {
			throw new Error("The stream reference is neither a Blob nor a typed array.");
		}
		const token = nextChunkToken++;
		stagedChunks.set(token, bytes);
		return token;
	}
});

// ---------- JSInterop helpers (former Avae.Browser.Essentials API) ----------

/** Synchronous POST (XMLHttpRequest); returns the response text, throws on a non-200 status. */
export function fetchSync(url, data) {
	const xhr = new XMLHttpRequest();
	xhr.open("POST", url, false);
	if (data !== null && data !== undefined)
		xhr.setRequestHeader("Content-type", "application/x-www-form-urlencoded");
	xhr.send(data ?? null);
	if (xhr.status === 200)
		return xhr.responseText;
	throw new Error(xhr.statusText || `HTTP ${xhr.status}`);
}

export function evaluate(code) {
	// Indirect eval: runs in the global scope, as globalThis.eval did in Avae.Browser.Essentials.
	const result = (0, eval)(code);
	return result === undefined || result === null ? null : String(result);
}

export function open(url, target) {
	globalThis.open(url, target || "_blank");
}
