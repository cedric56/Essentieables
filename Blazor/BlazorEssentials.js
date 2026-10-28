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
	const promise = new Promise(resolve => (resolvePromise = resolve));
	let closed = false;

	const onKeyDown = e => { if (e.key === "Escape") close(null); };
	const onBeforeUnload = () => close(null);

	function close(result) {
		if (closed) return;
		closed = true;

		onClose?.();
		document.removeEventListener("keydown", onKeyDown);
		window.removeEventListener("beforeunload", onBeforeUnload);
		overlay.remove();
		resolvePromise(result);
	}

	overlay.addEventListener("click", e => { if (e.target === overlay) close(null); });
	document.addEventListener("keydown", onKeyDown);
	window.addEventListener("beforeunload", onBeforeUnload);

	const $ = selector => modal.querySelector(selector);
	return { $, close, promise };
}

/** Requests the camera; returns the stream or null if denied/unavailable. */
async function openCamera({ audio = false } = {}) {
	try {
		return await navigator.mediaDevices.getUserMedia({
			video: VIDEO_SIZE,
			...(audio ? { audio: AUDIO_CONSTRAINTS } : {})
		});
	} catch (err) {
		console.error("Camera access denied:", err);
		return null;
	}
}

/* ---------- photo ---------- */

export function capturePhotoInPopup() {
	let stream = null;

	const { $, close, promise } = openPopup(`
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
			blob => close(blob ? URL.createObjectURL(blob) : null),
			"image/png"
		);
	}

	takeButton.addEventListener("click", takePhoto);
	$("#acceptPhoto").addEventListener("click", acceptPhoto);
	$("#rejectPhoto").addEventListener("click", showPreview);

	openCamera().then(s => {
		if (!s) return close(null);
		stream = s;
		video.srcObject = stream;
		setStatus("Live");
	});

	return promise;
}

/* ---------- video ---------- */

export function captureVideoInPopup() {
	let stream = null;
	let recorder = null;
	let chunks = [];
	let mimeType = "";

	const { $, close, promise } = openPopup(`
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
	`, { onClose: () => stopStream(stream) });

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
		stream = await openCamera({ audio: true });
		if (!stream) return close(null);
		preview.srcObject = stream;
		setStatus("Ready");
		hint.textContent = "Position yourself, then start recording.";
	}

	function startCapture() {
		if (!stream) return;

		startButton.disabled = true;
		stopButton.disabled = false;
		hide(actions);

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
		playback.src = URL.createObjectURL(buildBlob());
		playback.controls = true;
		playback.muted = false;
		playback.autoplay = false;

		hide(preview);
		show(playback);
		show(actions, "flex");
	}

	function acceptCapture() {
		close(URL.createObjectURL(buildBlob()));
	}

	function rejectCapture() {
		if (playback.src) URL.revokeObjectURL(playback.src);
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

export async function getBlobBytes(blobUrl) {
	const response = await fetch(blobUrl);
	const buffer = await response.arrayBuffer(); // no need for .blob() first
	URL.revokeObjectURL(blobUrl);
	return new Uint8Array(buffer);
}

// ---------- Contacts -----------

export function getAllContactsAsync(multiple) {
	return new Promise(async (resolve, reject) => {
		if ('ContactsManager' in window) {

			const opts = { multiple: multiple };
			const contacts = await navigator.contacts.select(["name", "email", "tel", "address"], opts);
			const contactsJson = contacts.map(voice => ({
				name: voice.name,
				email: voice.email,
				tel: voice.tel,
				address: voice.address,
			}));
			resolve(JSON.stringify(contactsJson, null, 2));
			return;
		}

		resolve('');

	});
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

async function ssGetOrCreateKey() {
	const db = await ssOpenDb();
	const existing = await new Promise((res, rej) => {
		const tx = db.transaction(SS_STORE, "readonly");
		const req = tx.objectStore(SS_STORE).get(SS_KEY_ID);
		req.onsuccess = () => res(req.result);
		req.onerror = () => rej(req.error);
	});
	if (existing) return existing;

	const key = await crypto.subtle.generateKey(
		{ name: "AES-GCM", length: 256 }, false, ["encrypt", "decrypt"]);

	await new Promise((res, rej) => {
		const tx = db.transaction(SS_STORE, "readwrite");
		tx.objectStore(SS_STORE).put(key, SS_KEY_ID);
		tx.oncomplete = () => res();
		tx.onerror = () => rej(tx.error);
	});
	return key;
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

	try {
		const key = await ssGetOrCreateKey();
		const { iv, data } = JSON.parse(raw);
		const plainBuf = await crypto.subtle.decrypt(
			{ name: "AES-GCM", iv: new Uint8Array(iv) },
			key,
			new Uint8Array(data));
		return new TextDecoder().decode(plainBuf);
	} catch {
		return null; // key regenerated or corrupted value
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

		globalThis.navigator.geolocation.getCurrentPosition(
			position => resolve(positionToPayload(position)),
			error => reject(
				new Error(error.code === 1 ? "permission" : error.message)
			),
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
					error.code === 1 ? "permission" : error.message)
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

export function openUrl(url) {
	// noopener so the opened page cannot script this app.
	return globalThis.window.open(url, '_blank', 'noopener') !== null;
}

export function navigateTo(url) {
	// Used for protocol-handler schemes (mailto:, tel:, sms:) — does not unload the app.
	globalThis.location.assign(url);
	return true;
}

export function openFileBlob(base64, contentType, name) {
	const blob = new Blob([base64ToBytes(base64)], { type: contentType || 'application/octet-stream' });
	const url = URL.createObjectURL(blob);
	const opened = globalThis.window.open(url, '_blank');
	// Give the new tab time to load before revoking.
	setTimeout(() => URL.revokeObjectURL(url), 60000);
	return opened !== null;
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
			//return "DeviceMotionEvent" in globalThis;

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
	const eventCtor = globalThis.DeviceOrientationEvent;
		// kind === "accelerometer" || kind === "gyroscope"
		// 	? globalThis.DeviceMotionEvent
		// 	: globalThis.DeviceOrientationEvent;

	if (typeof eventCtor.requestPermission === "function") {
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

export async function fetchAppFile(path) {
	const response = await globalThis.fetch(new URL(path, globalThis.document.baseURI), { method: 'GET' });
	if (!response.ok)
		return null;
	return bytesToBase64(new Uint8Array(await response.arrayBuffer()));
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
		dotNetRef.invokeMethodAsync("OnBrowserChanged", readConnectivity());
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
	appInfoHandler = e => dotNetRef.invokeMethodAsync("OnThemeChanged", e.matches);
	appInfoMql.addEventListener("change", appInfoHandler);
}

export function appInfoUnsubscribeTheme() {
	if (appInfoMql && appInfoHandler) {
		appInfoMql.removeEventListener("change", appInfoHandler);
	}
	appInfoMql = null;
	appInfoHandler = null;
}

export function pickFiles(title, accept, multiple) {
	return new Promise(resolve => {
		const input = document.createElement("input");
		input.type = "file";
		input.multiple = !!multiple;
		if (accept) input.accept = accept;
		if (title) {
			input.setAttribute("aria-label", title);
		}
		input.style.display = "none";

		input.addEventListener("change", async () => {
			const files = Array.from(input.files ?? []);
			const results = await Promise.all(files.map(f => new Promise((res, rej) => {
				const reader = new FileReader();
				reader.onload = async () => {
					// const base64 = reader.result.substring(reader.result.indexOf(",") + 1);
					// res({ name: f.name, type: f.type, dataBase64: base64 });
					const arrayBuffer = await f.arrayBuffer();
					const bytes = new Uint8Array(arrayBuffer);
					res({ name: f.name, type: f.type, data: bytes });
				};
				reader.onerror = () => rej(reader.error);
				reader.readAsDataURL(f);
			})));
			document.body.removeChild(input);
			resolve(results);
		});

		// Some browsers require the input to be in the DOM to show the dialog reliably.
		document.body.appendChild(input);
		input.click();
	});
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

			const finish = (error) => {
				if (finished) return;
				finished = true;
				cleanup();

				if (error) reject(error);
				else resolve(true);
			};

			const onMessage = (event) => {
				if (event.origin !== window.location.origin)
					return;

				if (event.source !== popup)
					return;

				if (event.data?.type === "web-authenticator-complete")
					finish();
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
