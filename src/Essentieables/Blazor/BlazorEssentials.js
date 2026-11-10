

// Browser Essentials interop module.
// Loaded by BrowserEssentials.InitializeAsync() via JSHost.ImportAsync and
// bound from C# with [JSImport]. All functions are plain ES module exports
// with no dependencies.

// ---------- Media Picker -----------

const IS_MOBILE = /Android|iPhone/i.test(navigator.userAgent);
const VIDEO_SIZE = IS_MOBILE ? { width: 320, height: 240 } : { width: 640, height: 480 };

const AUDIO_CONSTRAINTS = {
	echoCancellation: true,
	noiseSuppression: true,
	autoGainControl: true
};

const VIDEO_MIME_CANDIDATES = [
	"video/webm;codecs=vp8,opus",
	"video/webm",
	"video/mp4"
];

const OVERLAY_STYLE = {
	position: "fixed",
	top: "0",
	left: "0",
	width: "100%",
	height: "100%",
	background: "rgba(0,0,0,0.5)",
	zIndex: "9999",
	display: "flex",
	alignItems: "center",
	justifyContent: "center"
};

const MODAL_STYLE = {
	background: "#fff",
	padding: "10px",
	borderRadius: "8px",
	width: "90%",
	maxWidth: "480px",
	textAlign: "center"
};

/* ---------- helpers ---------- */

function stopStream(stream) {
	stream?.getTracks().forEach(track => track.stop());
}

function show(el, display = "block") {
	el.style.display = display;
}

function hide(el) {
	el.style.display = "none";
}

function pickVideoMimeType() {
	return VIDEO_MIME_CANDIDATES.find(type => MediaRecorder.isTypeSupported(type)) ?? "";
}

/**
 * Builds the overlay + modal, mounts it, and wires up the shared
 * "click outside / Escape to dismiss" behaviour.
 * Returns the modal element, a `close(result)` function and the promise.
 */
function openPopup(innerHtml, { onClose } = {}) {
	const overlay = document.createElement("div");
	Object.assign(overlay.style, OVERLAY_STYLE);

	const modal = document.createElement("div");
	Object.assign(modal.style, MODAL_STYLE);
	modal.innerHTML = innerHtml;

	overlay.appendChild(modal);
	document.body.appendChild(overlay);

	let resolvePromise;
	let rejectPromise;
	const promise = new Promise((resolve, reject) => { resolvePromise = resolve; rejectPromise = reject; });
	let closed = false;

	const onKeyDown = e => { if (e.key === "Escape") close(null); };
	const onBeforeUnload = () => close(null);

	/** Closes the popup; resolves with `result`, or rejects when `error` is given. */
	function close(result, error) {
		if (closed) return;
		closed = true;

		try { onClose?.(); } catch (e) { console.error(e); }
		document.removeEventListener("keydown", onKeyDown);
		window.removeEventListener("beforeunload", onBeforeUnload);
		overlay.remove();
		if (error) rejectPromise(error);
		else resolvePromise(result);
	}

	const isClosed = () => closed;

	overlay.addEventListener("click", e => { if (e.target === overlay) close(null); });
	document.addEventListener("keydown", onKeyDown);
	window.addEventListener("beforeunload", onBeforeUnload);

	const $ = selector => modal.querySelector(selector);
	return { $, close, isClosed, promise };
}

/** True when the camera can be requested in this context (secure context + getUserMedia). */
export function captureIsSupported() {
	return !!(globalThis.isSecureContext && navigator.mediaDevices?.getUserMedia);
}

/**
 * Requests the camera. Rejects with Error("permission") when the user or the browser policy
 * refuses it, Error("unsupported") when there is no camera API, Error(message) otherwise.
 */
async function openCamera({ audio = false } = {}) {
	if (!captureIsSupported())
		throw new Error("unsupported");
	try {
		return await navigator.mediaDevices.getUserMedia({
			video: VIDEO_SIZE,
			...(audio ? { audio: AUDIO_CONSTRAINTS } : {})
		});
	} catch (err) {
		if (err?.name === "NotAllowedError" || err?.name === "SecurityError")
			throw new Error("permission");
		if (err?.name === "NotFoundError" || err?.name === "OverconstrainedError")
			throw new Error("unsupported");
		throw new Error(err?.message || "camera");
	}
}

/* ---------- photo ---------- */

export function capturePhotoInPopup() {
	let stream = null;

	const { $, close, isClosed, promise } = openPopup(`
		<style>
			.cp-root {
				--cp-bg: #0f1115;
				--cp-surface: #1a1d24;
				--cp-border: rgba(255,255,255,.08);
				--cp-text: #f3f4f6;
				--cp-muted: #9ca3af;
				--cp-accent: #6366f1;
				--cp-success: #22c55e;
				font-family: system-ui, -apple-system, Segoe UI, Roboto, sans-serif;
				color: var(--cp-text);
				background: var(--cp-bg);
				border-radius: 16px;
				overflow: hidden;
				min-width: min(100%, 420px);
				max-width: 520px;
			}
			.cp-header {
				display: flex;
				align-items: center;
				justify-content: space-between;
				padding: 14px 16px;
				border-bottom: 1px solid var(--cp-border);
				background: var(--cp-surface);
			}
			.cp-title {
				margin: 0;
				font-size: 15px;
				font-weight: 600;
				letter-spacing: .01em;
			}
			.cp-badge {
				font-size: 11px;
				font-weight: 600;
				text-transform: uppercase;
				letter-spacing: .06em;
				padding: 4px 8px;
				border-radius: 999px;
				background: rgba(99,102,241,.15);
				color: #a5b4fc;
			}
			.cp-badge.review {
				background: rgba(34,197,94,.15);
				color: #86efac;
			}
			.cp-stage {
				position: relative;
				background: #000;
				aspect-ratio: 4 / 3;
			}
			.cp-stage video,
			.cp-stage canvas {
				display: block;
				width: 100%;
				height: 100%;
				object-fit: cover;
				background: #000;
			}
			.cp-footer {
				padding: 14px 16px 16px;
				border-top: 1px solid var(--cp-border);
				background: var(--cp-surface);
			}
			.cp-hint {
				margin: 0 0 12px;
				font-size: 12px;
				color: var(--cp-muted);
				text-align: center;
			}
			.cp-actions {
				display: flex;
				justify-content: center;
				gap: 10px;
				flex-wrap: wrap;
			}
			.cp-btn {
				appearance: none;
				border: 1px solid var(--cp-border);
				background: #252a33;
				color: var(--cp-text);
				font: inherit;
				font-size: 13px;
				font-weight: 600;
				padding: 10px 16px;
				border-radius: 10px;
				cursor: pointer;
				transition: transform .12s ease, background .12s ease, opacity .12s ease;
			}
			.cp-btn:hover:not(:disabled) { transform: translateY(-1px); background: #2d3340; }
			.cp-btn:active:not(:disabled) { transform: translateY(0); }
			.cp-btn:disabled { opacity: .4; cursor: not-allowed; }
			.cp-btn-primary {
				background: var(--cp-accent);
				border-color: transparent;
				color: #fff;
			}
			.cp-btn-primary:hover:not(:disabled) { background: #4f46e5; }
			.cp-btn-success {
				background: var(--cp-success);
				border-color: transparent;
				color: #052e16;
			}
			.cp-btn-success:hover:not(:disabled) { background: #16a34a; color: #fff; }
			.cp-btn-ghost {
				background: transparent;
			}
			.cp-shutter {
				width: 56px;
				height: 56px;
				border-radius: 50%;
				border: 3px solid rgba(255,255,255,.85);
				background: transparent;
				padding: 0;
				display: grid;
				place-items: center;
				cursor: pointer;
			}
			.cp-shutter::after {
				content: "";
				width: 42px;
				height: 42px;
				border-radius: 50%;
				background: #fff;
				transition: transform .12s ease;
			}
			.cp-shutter:hover::after { transform: scale(.92); }
			.cp-shutter:active::after { transform: scale(.84); }
		</style>
		<div class="cp-root">
			<div class="cp-header">
				<h3 class="cp-title">Take photo</h3>
				<span id="statusBadge" class="cp-badge">Live</span>
			</div>
			<div class="cp-stage">
				<video id="videoPreview" autoplay playsinline muted></video>
				<canvas id="photoCanvas" style="display:none;"></canvas>
			</div>
			<div class="cp-footer">
				<p id="hintText" class="cp-hint">Frame your shot, then capture.</p>
				<div id="captureControls" class="cp-actions">
					<button type="button" id="takePhoto" class="cp-shutter" aria-label="Take photo"></button>
				</div>
				<div id="actionButtons" class="cp-actions" style="display:none;">
					<button type="button" id="acceptPhoto" class="cp-btn cp-btn-success">Use photo</button>
					<button type="button" id="rejectPhoto" class="cp-btn cp-btn-ghost">Retake</button>
				</div>
			</div>
		</div>
	`, { onClose: () => stopStream(stream) });

	const video = $("#videoPreview");
	const canvas = $("#photoCanvas");
	const actions = $("#actionButtons");
	const captureControls = $("#captureControls");
	const takeButton = $("#takePhoto");
	const badge = $("#statusBadge");
	const hint = $("#hintText");

	const setStatus = (text, review = false) => {
		badge.textContent = text;
		badge.classList.toggle("review", review);
	};

	function showPreview() {
		hide(canvas);
		hide(actions);
		show(video);
		show(captureControls, "flex");
		setStatus("Live");
		hint.textContent = "Frame your shot, then capture.";
	}

	function showCaptured() {
		hide(video);
		hide(captureControls);
		show(canvas);
		show(actions, "flex");
		setStatus("Review", true);
		hint.textContent = "Looking good? Accept or retake.";
	}

	function takePhoto() {
		if (!stream) return;
		if (!video.videoWidth || !video.videoHeight) return;

		canvas.width = video.videoWidth;
		canvas.height = video.videoHeight;
		canvas.getContext("2d").drawImage(video, 0, 0, canvas.width, canvas.height);

		showCaptured();
	}

	function acceptPhoto() {
		canvas.toBlob(
			blob => close(blob ? { url: URL.createObjectURL(blob), type: blob.type || "image/png" } : null),
			"image/png"
		);
	}

	takeButton.addEventListener("click", takePhoto);
	$("#acceptPhoto").addEventListener("click", acceptPhoto);
	$("#rejectPhoto").addEventListener("click", showPreview);

	openCamera().then(s => {
		// The popup may have been dismissed while the permission prompt was open:
		// release the camera instead of leaving it running.
		if (isClosed()) return stopStream(s);
		stream = s;
		video.srcObject = stream;
		setStatus("Live");
	}, err => close(null, err));

	return promise;
}

/* ---------- video ---------- */

export function captureVideoInPopup() {
	let stream = null;
	let recorder = null;
	let chunks = [];
	let mimeType = "";
	let playbackUrl = null;

	const revokePlayback = () => {
		if (playbackUrl) URL.revokeObjectURL(playbackUrl);
		playbackUrl = null;
	};

	const { $, close, isClosed, promise } = openPopup(`
		<style>
			.cv-root {
				--cv-bg: #0f1115;
				--cv-surface: #1a1d24;
				--cv-border: rgba(255,255,255,.08);
				--cv-text: #f3f4f6;
				--cv-muted: #9ca3af;
				--cv-accent: #6366f1;
				--cv-danger: #ef4444;
				--cv-success: #22c55e;
				font-family: system-ui, -apple-system, Segoe UI, Roboto, sans-serif;
				color: var(--cv-text);
				background: var(--cv-bg);
				border-radius: 16px;
				overflow: hidden;
				min-width: min(100%, 420px);
				max-width: 520px;
			}
			.cv-header {
				display: flex;
				align-items: center;
				justify-content: space-between;
				padding: 14px 16px;
				border-bottom: 1px solid var(--cv-border);
				background: var(--cv-surface);
			}
			.cv-title {
				margin: 0;
				font-size: 15px;
				font-weight: 600;
				letter-spacing: .01em;
			}
			.cv-badge {
				font-size: 11px;
				font-weight: 600;
				text-transform: uppercase;
				letter-spacing: .06em;
				padding: 4px 8px;
				border-radius: 999px;
				background: rgba(99,102,241,.15);
				color: #a5b4fc;
			}
			.cv-badge.live {
				background: rgba(239,68,68,.15);
				color: #fca5a5;
				animation: cv-pulse 1.2s ease-in-out infinite;
			}
			@keyframes cv-pulse {
				0%, 100% { opacity: 1; }
				50% { opacity: .55; }
			}
			.cv-stage {
				position: relative;
				background: #000;
				aspect-ratio: 16 / 10;
			}
			.cv-stage video {
				display: block;
				width: 100%;
				height: 100%;
				object-fit: cover;
				background: #000;
			}
			.cv-rec-dot {
				position: absolute;
				top: 12px;
				left: 12px;
				display: none;
				align-items: center;
				gap: 6px;
				padding: 6px 10px;
				border-radius: 999px;
				background: rgba(0,0,0,.55);
				backdrop-filter: blur(6px);
				font-size: 12px;
				font-weight: 600;
			}
			.cv-rec-dot::before {
				content: "";
				width: 8px;
				height: 8px;
				border-radius: 50%;
				background: var(--cv-danger);
				box-shadow: 0 0 0 3px rgba(239,68,68,.25);
			}
			.cv-footer {
				padding: 14px 16px 16px;
				border-top: 1px solid var(--cv-border);
				background: var(--cv-surface);
			}
			.cv-hint {
				margin: 0 0 12px;
				font-size: 12px;
				color: var(--cv-muted);
				text-align: center;
			}
			.cv-actions {
				display: flex;
				justify-content: center;
				gap: 10px;
				flex-wrap: wrap;
			}
			.cv-btn {
				appearance: none;
				border: 1px solid var(--cv-border);
				background: #252a33;
				color: var(--cv-text);
				font: inherit;
				font-size: 13px;
				font-weight: 600;
				padding: 10px 16px;
				border-radius: 10px;
				cursor: pointer;
				transition: transform .12s ease, background .12s ease, opacity .12s ease;
			}
			.cv-btn:hover:not(:disabled) { transform: translateY(-1px); background: #2d3340; }
			.cv-btn:active:not(:disabled) { transform: translateY(0); }
			.cv-btn:disabled { opacity: .4; cursor: not-allowed; }
			.cv-btn-primary {
				background: var(--cv-accent);
				border-color: transparent;
				color: #fff;
			}
			.cv-btn-primary:hover:not(:disabled) { background: #4f46e5; }
			.cv-btn-danger {
				background: var(--cv-danger);
				border-color: transparent;
				color: #fff;
			}
			.cv-btn-danger:hover:not(:disabled) { background: #dc2626; }
			.cv-btn-success {
				background: var(--cv-success);
				border-color: transparent;
				color: #052e16;
			}
			.cv-btn-success:hover:not(:disabled) { background: #16a34a; color: #fff; }
			.cv-btn-ghost {
				background: transparent;
			}
		</style>
		<div class="cv-root">
			<div class="cv-header">
				<h3 class="cv-title">Record video</h3>
				<span id="statusBadge" class="cv-badge">Ready</span>
			</div>
			<div class="cv-stage">
				<video id="videoPreview" autoplay playsinline muted></video>
				<video id="playbackPreview" style="display:none;"></video>
				<div id="recIndicator" class="cv-rec-dot">REC</div>
			</div>
			<div class="cv-footer">
				<p id="hintText" class="cv-hint">Position yourself, then start recording.</p>
				<div id="recordingControls" class="cv-actions">
					<button type="button" id="startRecording" class="cv-btn cv-btn-primary">Start recording</button>
					<button type="button" id="stopRecording" class="cv-btn cv-btn-danger" disabled>Stop</button>
				</div>
				<div id="actionButtons" class="cv-actions" style="display:none;">
					<button type="button" id="acceptRecording" class="cv-btn cv-btn-success">Use this clip</button>
					<button type="button" id="rejectRecording" class="cv-btn cv-btn-ghost">Retake</button>
				</div>
			</div>
		</div>
	`, {
		onClose: () => {
			if (recorder && recorder.state !== "inactive") {
				recorder.onstop = null;
				recorder.stop();
			}
			stopStream(stream);
			revokePlayback();
		}
	});

	const preview = $("#videoPreview");
	const playback = $("#playbackPreview");
	const controls = $("#recordingControls");
	const actions = $("#actionButtons");
	const startButton = $("#startRecording");
	const stopButton = $("#stopRecording");
	const badge = $("#statusBadge");
	const recDot = $("#recIndicator");
	const hint = $("#hintText");

	const setStatus = (text, live = false) => {
		badge.textContent = text;
		badge.classList.toggle("live", live);
		recDot.style.display = live ? "flex" : "none";
	};

	const buildBlob = () => new Blob(chunks, { type: mimeType || "video/webm" });

	async function startCamera() {
		let s;
		try {
			s = await openCamera({ audio: true });
		} catch (err) {
			return close(null, err);
		}
		if (isClosed()) return stopStream(s);
		stream = s;
		preview.srcObject = stream;
		setStatus("Ready");
		hint.textContent = "Position yourself, then start recording.";
	}

	function startCapture() {
		if (!stream) return;

		startButton.disabled = true;
		stopButton.disabled = false;
		hide(actions);

		if (typeof MediaRecorder === "undefined")
			return close(null, new Error("unsupported"));

		chunks = [];
		mimeType = pickVideoMimeType();
		recorder = new MediaRecorder(stream, mimeType ? { mimeType } : undefined);
		recorder.ondataavailable = e => { if (e.data.size > 0) chunks.push(e.data); };
		recorder.onstop = showPlayback;
		recorder.start();

		setStatus("Recording", true);
		hint.textContent = "Recording… tap Stop when you’re done.";
	}

	function stopCapture() {
		if (recorder?.state !== "inactive") recorder?.stop();
		stopStream(stream);
		stopButton.disabled = true;
		hide(controls);
		setStatus("Review");
		hint.textContent = "Preview your clip, then accept or retake.";
	}

	function showPlayback() {
		revokePlayback();
		playbackUrl = URL.createObjectURL(buildBlob());
		playback.src = playbackUrl;
		playback.controls = true;
		playback.muted = false;
		playback.autoplay = false;

		hide(preview);
		show(playback);
		show(actions, "flex");
	}

	function acceptCapture() {
		const blob = buildBlob();
		close({ url: URL.createObjectURL(blob), type: blob.type || "video/webm" });
	}

	function rejectCapture() {
		revokePlayback();
		playback.removeAttribute("src");
		playback.muted = true;
		chunks = [];

		hide(playback);
		hide(actions);
		show(preview);
		show(controls, "flex");
		startButton.disabled = false;

		setStatus("Ready");
		hint.textContent = "Camera restarted — record again when ready.";
		startCamera();
	}

	startButton.addEventListener("click", startCapture);
	stopButton.addEventListener("click", stopCapture);
	$("#acceptRecording").addEventListener("click", acceptCapture);
	$("#rejectRecording").addEventListener("click", rejectCapture);

	startCamera();
	return promise;
}

/**
 * Returns the content of a blob: URL created by the capture popups and revokes the URL.
 * Received in .NET as an IJSStreamReference (streamed, so it is not limited by the SignalR
 * message size on Blazor Server).
 */
export async function takeBlob(blobUrl) {
	try {
		const response = await fetch(blobUrl);
		return await response.blob();
	} finally {
		URL.revokeObjectURL(blobUrl);
	}
}

// ---------- Contacts -----------

export function contactsIsSupported() {
	return 'contacts' in navigator && 'ContactsManager' in window;
}

/** Opens the Contact Picker. Rejections (permission, user activation…) reach .NET as JSException. */
export async function getAllContactsAsync(multiple) {
	if (!contactsIsSupported())
		throw new Error("unsupported");

	const contacts = await navigator.contacts.select(["name", "email", "tel", "address"], { multiple: !!multiple });
	return JSON.stringify(contacts.map(c => ({
		name: c.name,
		email: c.email,
		tel: c.tel,
		address: c.address,
	})));
}

// ---------- Preferences (localStorage) ----------

export function prefsGetAll(prefix) {
	const result = {};
	for (let i = 0; i < localStorage.length; i++) {
		const k = localStorage.key(i);
		if (k && k.startsWith(prefix)) result[k] = localStorage.getItem(k);
	}
	return result;
}

export function prefsSet(key, value) { localStorage.setItem(key, value); }
export function prefsRemove(key) { localStorage.removeItem(key); }
export function prefsClear(prefix) {
	const keys = [];
	for (let i = 0; i < localStorage.length; i++) {
		const k = localStorage.key(i);
		if (k && k.startsWith(prefix)) keys.push(k);
	}
	keys.forEach(k => localStorage.removeItem(k));
}


function bytesToBase64(bytes) {
	let binary = '';
	const chunk = 0x8000;
	for (let i = 0; i < bytes.length; i += chunk)
		binary += String.fromCharCode.apply(null, bytes.subarray(i, i + chunk));
	return btoa(binary);
}

function base64ToBytes(base64) {
	const binary = atob(base64);
	const bytes = new Uint8Array(binary.length);
	for (let i = 0; i < binary.length; i++)
		bytes[i] = binary.charCodeAt(i);
	return bytes;
}

// ---------- Secure storage (AES-GCM via WebCrypto, key in IndexedDB) ----------

const SS_DB = "maui-securestorage";
const SS_STORE = "keys";
const SS_KEY_ID = "aes-key";

function ssOpenDb() {
	return new Promise((resolve, reject) => {
		const req = indexedDB.open(SS_DB, 1);
		req.onupgradeneeded = () => req.result.createObjectStore(SS_STORE);
		req.onsuccess = () => resolve(req.result);
		req.onerror = () => reject(req.error);
	});
}

function ssReadKey(db) {
	return new Promise((res, rej) => {
		const tx = db.transaction(SS_STORE, "readonly");
		const req = tx.objectStore(SS_STORE).get(SS_KEY_ID);
		req.onsuccess = () => res(req.result);
		req.onerror = () => rej(req.error);
	});
}

let ssKeyPromise = null;

/**
 * Returns the AES key, creating it on first use. Two tabs may both find no key: the candidate
 * is only stored by a readwrite transaction that checks again for an existing key, and readwrite
 * transactions on the same store are serialized by IndexedDB across tabs, so every tab ends up
 * with the same key (no value is encrypted with a key that is later overwritten).
 */
function ssGetOrCreateKey() {
	if (!ssKeyPromise) {
		ssKeyPromise = (async () => {
			if (!globalThis.indexedDB || !globalThis.crypto?.subtle)
				throw new Error("unsupported");

			const db = await ssOpenDb();
			const existing = await ssReadKey(db);
			if (existing) return existing;

			const candidate = await crypto.subtle.generateKey(
				{ name: "AES-GCM", length: 256 }, false, ["encrypt", "decrypt"]);

			return await new Promise((res, rej) => {
				const tx = db.transaction(SS_STORE, "readwrite");
				const store = tx.objectStore(SS_STORE);
				let key = candidate;
				const req = store.get(SS_KEY_ID);
				req.onsuccess = () => {
					if (req.result) key = req.result;          // another tab won the race
					else store.add(candidate, SS_KEY_ID);
				};
				tx.oncomplete = () => res(key);
				tx.onerror = () => rej(tx.error);
				tx.onabort = () => rej(tx.error);
			});
		})();
		// Retry on the next call after a failure instead of caching the rejection.
		ssKeyPromise.catch(() => { ssKeyPromise = null; });
	}
	return ssKeyPromise;
}

export function secureKeys(prefix) {
	const keys = [];
	for (let i = 0; i < localStorage.length; i++) {
		const k = localStorage.key(i);
		if (k && k.startsWith(prefix)) keys.push(k);
	}
	return keys;
}

export async function secureGet(storageKey) {
	const raw = localStorage.getItem(storageKey);
	if (!raw) return null;

	// Key or storage failures propagate to .NET. Only an undecryptable value (key lost after
	// site data was partly cleared, corrupted entry) reads as missing, as on the native platforms.
	const key = await ssGetOrCreateKey();
	try {
		const { iv, data } = JSON.parse(raw);
		const plainBuf = await crypto.subtle.decrypt(
			{ name: "AES-GCM", iv: new Uint8Array(iv) },
			key,
			new Uint8Array(data));
		return new TextDecoder().decode(plainBuf);
	} catch {
		localStorage.removeItem(storageKey);
		return null;
	}
}

export async function secureSet(storageKey, value) {
	const key = await ssGetOrCreateKey();
	const iv = crypto.getRandomValues(new Uint8Array(12));
	const cipherBuf = await crypto.subtle.encrypt(
		{ name: "AES-GCM", iv },
		key,
		new TextEncoder().encode(value));

	localStorage.setItem(storageKey, JSON.stringify({
		iv: Array.from(iv),
		data: Array.from(new Uint8Array(cipherBuf))
	}));
}

export function secureRemove(storageKey) {
	localStorage.removeItem(storageKey);
}

/** Removes every entry with the prefix, including the ones written by other tabs. */
export function secureRemoveAll(prefix) {
	for (const k of secureKeys(prefix))
		localStorage.removeItem(k);
}

// ---------- Clipboard ----------

export function clipboardWriteText(text) {
	return globalThis.navigator.clipboard.writeText(text);
}

export function clipboardReadText() {
	return globalThis.navigator.clipboard.readText();
}

// ---------- Device info ----------

export function getDeviceInfo() {
	const nav = globalThis.navigator;
	const uaData = nav.userAgentData;
	return {
		userAgent: nav.userAgent || '',
		vendor: nav.vendor || '',
		language: nav.language || '',
		platform: (uaData && uaData.platform) || nav.platform || '',
		mobile: uaData ? !!uaData.mobile : /Mobi|Android|iPhone|iPad/i.test(nav.userAgent || ''),
		brands: (uaData && uaData.brands) ? uaData.brands.map(b => ({ brand: b.brand, version: b.version })) : []
	};
}
// ---------- Display ----------
function ddGetInfo() {
	const o = screen.orientation;
	return {
		width: window.innerWidth,
		height: window.innerHeight,
		pixelRatio: window.devicePixelRatio || 1,
		orientationType: o?.type ?? ""
	};
}

let ddWakeLock = null;
let ddWakeLockRequested = false;

async function ddAcquireWakeLock() {
	if (!("wakeLock" in navigator)) return false;

	try {
		ddWakeLock = await navigator.wakeLock.request("screen");
		ddWakeLock.addEventListener("release", () => {
			ddWakeLock = null;
		});
		return true;
	} catch {
		return false;
	}
}

function ddReleaseWakeLock() {
	ddWakeLock?.release();
	ddWakeLock = null;
}

export function ddGetSnapshot() {
	return {
		...ddGetInfo(),
		wakeLockActive: ddWakeLock !== null
	};
}

export async function ddSetWakeLock(dotNetRef, on) {
	ddWakeLockRequested = on;

	if (on) {
		const ok = await ddAcquireWakeLock();
		await dotNetRef.invokeMethodAsync("OnWakeLockChanged", ok);
	} else {
		ddReleaseWakeLock();
		await dotNetRef.invokeMethodAsync("OnWakeLockChanged", false);
	}
}

// Keep the exact handlers so they can be removed later.
let ddSubscription = null;

export function ddSubscribe(dotNetRef) {
	// Prevent duplicate subscriptions.
	ddUnsubscribe();

	const notify = () => {
		dotNetRef
			.invokeMethodAsync("OnDisplayChanged", ddGetInfo())
			.catch(() => { });
	};

	const visibilityChanged = async () => {
		// Wake locks are released by the browser when the page is hidden;
		// re-request when the page becomes visible again.
		if (
			document.visibilityState === "visible" &&
			ddWakeLockRequested &&
			!ddWakeLock
		) {
			const ok = await ddAcquireWakeLock();

			try {
				await dotNetRef.invokeMethodAsync("OnWakeLockChanged", ok);
			} catch {
				// The .NET object may have been disposed while the request
				// was in progress.
			}
		}
	};

	window.addEventListener("resize", notify);
	screen.orientation?.addEventListener("change", notify);
	document.addEventListener("visibilitychange", visibilityChanged);

	ddSubscription = {
		notify,
		visibilityChanged
	};
}

export function ddUnsubscribe() {
	if (ddSubscription) {
		window.removeEventListener("resize", ddSubscription.notify);
		screen.orientation?.removeEventListener(
			"change",
			ddSubscription.notify
		);
		document.removeEventListener(
			"visibilitychange",
			ddSubscription.visibilityChanged
		);

		ddSubscription = null;
	}

	ddWakeLockRequested = false;
	ddReleaseWakeLock();
}
// ---------- Geolocation ----------

// GeolocationPositionError codes → names understood by BlazorGeolocation.
function geoErrorName(error) {
	switch (error?.code) {
		case 1: return "permission";
		case 2: return "unavailable";
		case 3: return "timeout";
		default: return error?.message || "unknown";
	}
}

export async function geoIsEnabled() {
	if (!globalThis.navigator.geolocation || !globalThis.isSecureContext)
		return false;
	try {
		const status = await navigator.permissions?.query({ name: "geolocation" });
		return status?.state !== "denied";
	} catch {
		return true; // Permissions API not available: the browser will ask.
	}
}

function positionToPayload(position) {
	const c = position.coords;

	return {
		latitude: c.latitude,
		longitude: c.longitude,
		accuracy: c.accuracy,
		altitude: c.altitude,
		altitudeAccuracy: c.altitudeAccuracy,
		heading: c.heading,
		speed: c.speed,
		timestamp: position.timestamp
	};
}

export function geoGetCurrentPosition(enableHighAccuracy, timeoutMs) {
	return new Promise((resolve, reject) => {
		if (!globalThis.navigator.geolocation) {
			reject(new Error("unsupported"));
			return;
		}
		if (!globalThis.isSecureContext) {
			reject(new Error("insecure"));
			return;
		}

		globalThis.navigator.geolocation.getCurrentPosition(
			position => resolve(positionToPayload(position)),
			error => reject(new Error(geoErrorName(error))),
			{
				enableHighAccuracy,
				timeout: timeoutMs > 0 ? timeoutMs : Infinity,
				maximumAge: 0
			});
	});
}

export function geoWatchStart(dotNetRef, enableHighAccuracy) {
	if (!globalThis.navigator.geolocation)
		return -1;

	return globalThis.navigator.geolocation.watchPosition(
		position => {
			dotNetRef
				.invokeMethodAsync(
					"OnGeolocationChanged",
					positionToPayload(position))
				.catch(() => { });
		},
		error => {
			dotNetRef
				.invokeMethodAsync(
					"OnGeolocationError",
					geoErrorName(error))
				.catch(() => { });
		},
		{
			enableHighAccuracy
		});
}

export function geoWatchStop(watchId) {
	if (globalThis.navigator.geolocation && watchId >= 0)
		globalThis.navigator.geolocation.clearWatch(watchId);
}

// ---------- Battery ----------

let batteryManager = null;

const batterySubscriptions = new Map();

async function batGetManager() {
	if (!("getBattery" in navigator))
		return null;

	if (!batteryManager)
		batteryManager = await navigator.getBattery();

	return batteryManager;
}

function batSnapshotOf(b) {
	return {
		level: b.level,
		charging: b.charging
	};
}

export async function batGetSnapshot() {
	const b = await batGetManager();
	return b ? batSnapshotOf(b) : null;
}

export async function batSubscribe(dotNetRef) {
	const b = await batGetManager();

	if (!b)
		return null;

	// Prevent duplicate subscriptions for the same .NET reference.
	await batUnsubscribe(dotNetRef);

	const notify = () =>
		dotNetRef.invokeMethodAsync(
			"OnBatteryChanged",
			batSnapshotOf(b)
		).catch(() => {
			// The .NET object may already have been disposed.
		});

	b.addEventListener("levelchange", notify);
	b.addEventListener("chargingchange", notify);

	batterySubscriptions.set(dotNetRef, {
		battery: b,
		notify
	});

	return true;
}

export async function batUnsubscribe(dotNetRef) {
	const subscription = batterySubscriptions.get(dotNetRef);

	if (!subscription)
		return;

	const { battery, notify } = subscription;

	battery.removeEventListener("levelchange", notify);
	battery.removeEventListener("chargingchange", notify);

	batterySubscriptions.delete(dotNetRef);
}

// ---------- Vibration / haptics ----------

export function vibrationIsSupported() {
	return typeof globalThis.navigator.vibrate === 'function';
}

export function vibrate(durationMs) {
	if (typeof globalThis.navigator.vibrate === 'function')
		globalThis.navigator.vibrate(durationMs);
}

// ---------- Share ----------

export function shareIsSupported() {
	return typeof globalThis.navigator.share === 'function';
}

export function share(title, text, url) {
	const data = {};
	if (title) data.title = title;
	if (text) data.text = text;
	if (url) data.url = url;
	return globalThis.navigator.share(data);
}

export function shareFiles(title, namesJson, typesJson, base64Json) {
	const names = JSON.parse(namesJson);
	const types = JSON.parse(typesJson);
	const contents = JSON.parse(base64Json);
	const files = names.map((name, i) => new File([base64ToBytes(contents[i])], name, { type: types[i] || 'application/octet-stream' }));
	if (!globalThis.navigator.canShare || !globalThis.navigator.canShare({ files }))
		return Promise.reject(new Error('unsupported'));
	const data = { files };
	if (title) data.title = title;
	return globalThis.navigator.share(data);
}

// ---------- Launcher / browser ----------

const WEB_SCHEMES = ["http:", "https:"];
const NAVIGATION_SCHEMES = ["mailto:", "tel:", "sms:"];

function hasScheme(url, schemes) {
	try {
		return schemes.includes(new URL(url, globalThis.document.baseURI).protocol);
	} catch {
		return false;
	}
}

export function openUrl(url) {
	// Checked again here: javascript:, data:, blob:, file:… are never opened.
	if (!hasScheme(url, WEB_SCHEMES))
		return false;
	// window.open(..., "noopener") always returns null, so the popup blocker could not be detected:
	// open a blank window, cut the opener, then navigate.
	const w = globalThis.window.open("about:blank", "_blank");
	if (!w)
		return false;
	w.opener = null;
	w.location.href = url;
	return true;
}

export function navigateTo(url) {
	// Used for protocol-handler schemes (mailto:, tel:, sms:) — does not unload the app.
	if (!hasScheme(url, NAVIGATION_SCHEMES))
		return false;
	globalThis.location.assign(url);
	return true;
}

// A blob: URL has the origin of the app: an HTML/SVG/XML file opened from one could run script
// with access to this origin (localStorage, the SecureStorage key…). Those types are downloaded.
const ACTIVE_CONTENT = /^(text\/html|application\/xhtml\+xml|image\/svg\+xml|text\/xml|application\/xml|text\/javascript|application\/javascript|application\/x-javascript)\b/i;

export async function openFileStream(streamRef, contentType, name) {
	const type = contentType || 'application/octet-stream';
	const blob = new Blob([await streamRef.arrayBuffer()], { type: ACTIVE_CONTENT.test(type) ? 'application/octet-stream' : type });
	const url = URL.createObjectURL(blob);
	// Give the new tab time to load before revoking.
	setTimeout(() => URL.revokeObjectURL(url), 60000);

	if (ACTIVE_CONTENT.test(type)) {
		const a = document.createElement("a");
		a.href = url;
		a.download = name || "download";
		a.rel = "noopener";
		document.body.appendChild(a);
		a.click();
		a.remove();
		return true;
	}

	const w = globalThis.window.open(url, '_blank');
	if (!w)
		return false;
	w.opener = null;
	return true;
}


// ---------- Text to speech ----------

export function speechGetVoices() {
	const synth = globalThis.speechSynthesis;
	if (!synth)
		return JSON.stringify([]);
	return JSON.stringify(synth.getVoices().map(v => ({ name: v.name, lang: v.lang, isDefault: v.default })));
}

export function speak(text, lang, pitch, rate, volume) {
	return new Promise((resolve, reject) => {
		const synth = globalThis.speechSynthesis;
		if (!synth) {
			reject(new Error('unsupported'));
			return;
		}
		const utterance = new SpeechSynthesisUtterance(text);
		if (lang) utterance.lang = lang;
		if (pitch >= 0) utterance.pitch = pitch;
		if (rate >= 0) utterance.rate = rate;
		if (volume >= 0) utterance.volume = volume;
		utterance.onend = () => resolve();
		utterance.onerror = e => e.error === 'canceled' || e.error === 'interrupted' ? resolve() : reject(new Error(e.error));
		synth.speak(utterance);
	});
}

export function speechCancel() {
	if (globalThis.speechSynthesis)
		globalThis.speechSynthesis.cancel();
}

// ---------- Sensors (devicemotion / deviceorientation) ----------

const DEG_TO_RAD = Math.PI / 180;
const GRAVITY = 9.80665;
const sensorHandlers = {};

function orientationToQuaternion(e) {
	const x = (e.beta || 0) * DEG_TO_RAD / 2;
	const y = (e.gamma || 0) * DEG_TO_RAD / 2;
	const z = (e.alpha || 0) * DEG_TO_RAD / 2;

	const cX = Math.cos(x), cY = Math.cos(y), cZ = Math.cos(z);
	const sX = Math.sin(x), sY = Math.sin(y), sZ = Math.sin(z);

	return {
		x: sX * cY * cZ - cX * sY * sZ,
		y: cX * sY * cZ + sX * cY * sZ,
		z: cX * cY * sZ + sX * sY * cZ,
		w: cX * cY * cZ - sX * sY * sZ
	};
}

export function sensorIsSupported(kind) {
	switch (kind) {
		case "accelerometer":
		case "gyroscope":
			return "DeviceMotionEvent" in globalThis;

		case "orientation":
		case "compass":
			return "DeviceOrientationEvent" in globalThis;

		case "magnetometer":
			return "magnetometer" in navigator;

		default:
			return false;
	}
}
let magSensor = null;

export async function sensorStart(dotNetRef, kind, frequencyHz) {
	if (!sensorIsSupported(kind) || sensorHandlers[kind])
		return false;

	// iOS Safari requires an explicit permission request.
	const eventCtor =
		kind === "accelerometer" || kind === "gyroscope"
			? globalThis.DeviceMotionEvent
			: globalThis.DeviceOrientationEvent;

	if (typeof eventCtor?.requestPermission === "function") {
		const state = await eventCtor.requestPermission();

		if (state !== "granted")
			return false;
	}

	let eventName;
	let handler;

	const notify = payload =>
		dotNetRef
			.invokeMethodAsync("OnReading", JSON.stringify(payload))
			.catch(err => dotNetRef
				.invokeMethodAsync("OnError", JSON.stringify(err)));

	switch (kind) {
		case "accelerometer":
			eventName = "devicemotion";
			handler = e => {
				const a = e.accelerationIncludingGravity;

				if (a) {
					notify({
						x: (a.x || 0) / GRAVITY,
						y: (a.y || 0) / GRAVITY,
						z: (a.z || 0) / GRAVITY
					});
				}
			};
			break;

		case "gyroscope":
			eventName = "devicemotion";
			handler = e => {
				const r = e.rotationRate;

				if (r) {
					notify({
						x: (r.beta || 0) * DEG_TO_RAD,
						y: (r.gamma || 0) * DEG_TO_RAD,
						z: (r.alpha || 0) * DEG_TO_RAD
					});
				}
			};
			break;

		case "orientation":
			eventName = "deviceorientation";
			handler = e => notify(orientationToQuaternion(e));
			break;

		case "compass":
			eventName =
				"ondeviceorientationabsolute" in globalThis
					? "deviceorientationabsolute"
					: "deviceorientation";

			handler = e => {
				const heading =
					typeof e.webkitCompassHeading === "number"
						? e.webkitCompassHeading
						: (e.absolute && e.alpha !== null
							? (360 - e.alpha) % 360
							: null);

				if (heading !== null)
					notify({ heading });
			};
			break;

		case "magnetometer":
			eventName = "reading";
			magSensor = new Magnetometer({ frequency: frequencyHz });
			handler = () => notify({
				x: magSensor.x ?? 0,
				y: magSensor.y ?? 0,
				z: magSensor.z ?? 0
			});
			magSensor.addEventListener("reading", handler);
			magSensor.start();
			break;

		default:
			return false;
	}

	sensorHandlers[kind] = {
		eventName,
		handler
	};

	if (kind !== "magnetometer")
		globalThis.addEventListener(eventName, handler);
	
	return true;
}

export function sensorStop(kind) {
	const entry = sensorHandlers[kind];

	if (entry) {

		if (kind === "magnetometer" && magSensor !== null) {
			magSensor.removeEventListener(entry.eventName, entry.handler);
			magSensor = null;
		}
		else {
			globalThis.removeEventListener(
				entry.eventName,
				entry.handler
			);
		}
		delete sensorHandlers[kind];
	}
}

// ---------- App package files (fetch relative to base URL) ----------

/** Returns the file as a Blob (an IJSStreamReference in .NET), or null when it does not exist. */
export async function fetchAppFile(path) {
	const response = await globalThis.fetch(new URL(path, globalThis.document.baseURI), { method: 'GET' });
	if (!response.ok)
		return null;
	return await response.blob();
}

export async function appFileExists(path) {
	try {
		const response = await globalThis.fetch(new URL(path, globalThis.document.baseURI), { method: 'HEAD' });
		return response.ok;
	} catch {
		return false;
	}
}

// ---------- Screen reader announcements (aria-live) ----------

let ariaLiveRegion = null;

export function announce(text) {
	const doc = globalThis.document;
	if (!ariaLiveRegion) {
		ariaLiveRegion = doc.createElement('div');
		ariaLiveRegion.setAttribute('aria-live', 'polite');
		ariaLiveRegion.setAttribute('role', 'status');
		ariaLiveRegion.style.cssText = 'position:absolute;width:1px;height:1px;margin:-1px;padding:0;overflow:hidden;clip:rect(0 0 0 0);white-space:nowrap;border:0;';
		doc.body.appendChild(ariaLiveRegion);
	}
	// Clear then set so repeated identical announcements are re-read.
	ariaLiveRegion.textContent = '';
	globalThis.setTimeout(() => { ariaLiveRegion.textContent = text; }, 50);
}
















let connectivityHandler = null;

function readConnectivity() {
	const c = navigator.connection;
	return {
		online: navigator.onLine,
		type: c?.type ?? "unknown",       // Chromium only: wifi, cellular, ethernet, bluetooth...
		saveData: c?.saveData ?? false
	};
}

export function connectivityGetSnapshot() {
	return readConnectivity();
}

export function connectivitySubscribe(dotNetRef) {
	connectivityUnsubscribe();
	connectivityHandler = () =>
		dotNetRef.invokeMethodAsync("OnBrowserChanged", readConnectivity()).catch(() => { });
	window.addEventListener("online", connectivityHandler);
	window.addEventListener("offline", connectivityHandler);
	navigator.connection?.addEventListener("change", connectivityHandler);
}

export function connectivityUnsubscribe() {
	if (!connectivityHandler) return;
	window.removeEventListener("online", connectivityHandler);
	window.removeEventListener("offline", connectivityHandler);
	navigator.connection?.removeEventListener("change", connectivityHandler);
	connectivityHandler = null;
}

export function appInfoGet() {
	return {
		title: document.title,
		hostname: location.hostname,
		rtl: document.documentElement.dir === "rtl"
			|| getComputedStyle(document.documentElement).direction === "rtl",
		prefersDark: window.matchMedia("(prefers-color-scheme: dark)").matches
	};
}

let appInfoMql = null;
let appInfoHandler = null;

export function appInfoSubscribeTheme(dotNetRef) {
	appInfoUnsubscribeTheme();
	appInfoMql = window.matchMedia("(prefers-color-scheme: dark)");
	appInfoHandler = e => dotNetRef.invokeMethodAsync("OnThemeChanged", e.matches).catch(() => { });
	appInfoMql.addEventListener("change", appInfoHandler);
}

export function appInfoUnsubscribeTheme() {
	if (appInfoMql && appInfoHandler) {
		appInfoMql.removeEventListener("change", appInfoHandler);
	}
	appInfoMql = null;
	appInfoHandler = null;
}

// Files picked by the user, kept until .NET has streamed them (see BlazorFilePicker).
const pickedFiles = new Map();
let pickSequence = 0;

/**
 * Shows the file dialog. Resolves with null when the user cancels, otherwise with
 * { token, files: [{ name, type, size }] }; the contents are then read with pickedFileData().
 */
export function pickFiles(title, accept, multiple) {
	return new Promise((resolve, reject) => {
		const input = document.createElement("input");
		input.type = "file";
		input.multiple = !!multiple;
		if (accept) input.accept = accept;
		if (title) input.setAttribute("aria-label", title);
		input.style.display = "none";

		let settled = false;
		const finish = files => {
			if (settled) return;
			settled = true;
			input.remove();
			if (!files.length) {
				resolve(null);
				return;
			}
			const token = ++pickSequence;
			pickedFiles.set(token, files);
			resolve({ token, files: files.map(f => ({ name: f.name, type: f.type, size: f.size })) });
		};

		input.addEventListener("change", () => finish(Array.from(input.files ?? [])));
		// Fired when the dialog is closed without a selection (Chrome 113+, Firefox 91+, Safari 16.4+).
		input.addEventListener("cancel", () => finish([]));

		// Some browsers require the input to be in the DOM to show the dialog reliably.
		document.body.appendChild(input);
		try {
			input.click();
		} catch (e) {
			settled = true;
			input.remove();
			reject(e);
		}
	});
}

/** The picked File (a Blob, streamed to .NET as an IJSStreamReference). */
export function pickedFileData(token, index) {
	const file = pickedFiles.get(token)?.[index];
	if (!file)
		throw new Error("The picked file is no longer available.");
	return file;
}

export function releasePickedFiles(token) {
	pickedFiles.delete(token);
}

// ---------- WebAuthenticator ----------

export function authenticate(url) {
		return new Promise((resolve, reject) => {
			const popup = window.open(
				"about:blank",
				"webAuthenticator",
				"width=520,height=700"
			);

			if (!popup) {
				reject(new Error("The authentication popup was blocked."));
				return;
			}

			let finished = false;

			const cleanup = () => {
				window.removeEventListener("message", onMessage);
				clearInterval(poll);
				clearTimeout(timeout);
			};

			const finish = (error, callbackUrl) => {
				if (finished) return;
				finished = true;
				cleanup();

				if (error) reject(error);
				else resolve(callbackUrl ?? null);
			};

			const onMessage = (event) => {
				if (event.origin !== window.location.origin)
					return;

				if (event.source !== popup)
					return;

				if (event.data?.type === "web-authenticator-complete")
					finish(null, event.data.url);
			};

			window.addEventListener("message", onMessage);

			const poll = setInterval(() => {
				if (popup.closed)
					finish(new Error("Authentication window closed."));
			}, 500);

			const timeout = setTimeout(() => {
				try { popup.close(); } catch { }
				finish(new Error("Authentication timed out."));
			}, 120000);

			// Open the authentication URL only after installing listeners.
			popup.location.href = url;
		});
}

// ---------- Torch ----------
let stream = null;

export async function isTorchSupported() {
	return navigator.mediaDevices?.getUserMedia &&
		navigator.mediaDevices.getSupportedConstraints().torch;
}

export async function on() {
	if (!navigator.mediaDevices?.getUserMedia) {
		throw new Error("Camera access is not supported.");
	}

	if (!navigator.mediaDevices.getSupportedConstraints().torch) {
		throw new Error("Flashlight control is not supported.");
	}

	// Avoid opening the camera more than once.
	if (stream) {
		const track = stream.getVideoTracks()[0];

		if (track?.readyState === "live") {
			await track.applyConstraints({
				advanced: [{ torch: true }]
			});
			return;
		}

		stream = null;
	}

	stream = await navigator.mediaDevices.getUserMedia({
		video: {
			facingMode: { ideal: "environment" }
		}
	});

	const track = stream.getVideoTracks()[0];

	try {
		await track.applyConstraints({
			advanced: [{ torch: true }]
		});
	} catch (error) {
		stream.getTracks().forEach(t => t.stop());
		stream = null;
		throw error;
	}
}

export async function off() {
	if (!stream) return;

	const currentStream = stream;
	stream = null;

	try {
		const track = currentStream.getVideoTracks()[0];

		if (track?.readyState === "live") {
			await track.applyConstraints({
				advanced: [{ torch: false }]
			});
		}
	} finally {
		currentStream.getTracks().forEach(t => t.stop());
	}
}
