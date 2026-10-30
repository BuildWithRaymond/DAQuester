import { createHash } from 'node:crypto';

/** Structural subset of the existing authoritative worker ledger. */
export interface LedgerOperation {
  id: string; kind: string; owner: string; status: 'pending' | 'complete' | 'review'; createdAt: number; data: Record<string, any>;
}
export interface SupplyLedger {
  operations(): LedgerOperation[];
  operation(kind: string, owner: string, data: any): LedgerOperation | Promise<LedgerOperation>;
  saveOperation(operation: LedgerOperation): void | Promise<void>;
}
export interface Unit {
  id: string; item: string; sourceAccepted: boolean; recipientObserved: boolean; cancelled: boolean;
  collected: boolean; banked: boolean; clientReported?: boolean;
}
export interface OperationStatus { sequence: number; state: string; errorCode: string | null; units: Omit<Unit, 'item' | 'clientReported'>[]; }
export interface CatalogPublication {
  ready: boolean; returnSupported: boolean; receiveModes: string[];
  items: { item: string; available: number; maxPerRequest: number }[];
}
export interface AdapterOptions {
  origin: string; token: string; gameServer: string; ledger: SupplyLedger;
  whisperQualified?: boolean; dispatchEnabled?: boolean; now?: () => number;
  withExclusiveOwner(work: () => Promise<any>): Promise<any>;
  /** Must atomically reserve against all existing work and enforce private stock/fee/character budgets. */
  reserveSupply?(operation: any): Promise<OperationStatus>;
  registerReturn?(operation: any): Promise<OperationStatus>;
  reconcile?(operation: any): Promise<OperationStatus>;
  /** Inject only for synthetic tests; production uses the bounded HTTPS transport. */
  request?(path: string, body: unknown): Promise<any>;
}
export interface WhisperObservation {
  channel: number; text: string; outgoingEcho: boolean; gameServer: string; recipient: string; observedAt: number;
}

export class SupplyWorkerAdapter {
  private options: AdapterOptions;
  private busy = false;
  private request: (path: string, body: unknown) => Promise<any>;
  private now: () => number;
  constructor(options: AdapterOptions) {
    let origin: URL;
    try { origin = new URL(options.origin); } catch { throw new Error('INVALID_CONFIGURATION'); }
    if (origin.protocol !== 'https:' || origin.username || origin.password || origin.pathname !== '/' || origin.search || origin.hash
      || !/^[A-Za-z0-9_.-]{1,128}$/.test(options.gameServer) || !/^[A-Za-z0-9_-]{32,128}$/.test(options.token))
      throw new Error('INVALID_CONFIGURATION');
    this.options = options;
    this.now = options.now ?? Date.now;
    this.request = options.request ?? ((path, body) => httpsRequest(origin.origin, options.token, path, body));
  }

  /** Publish only a private confirmed-availability projection, never aggregate collector totals. */
  async publishCatalog(catalog: CatalogPublication): Promise<void> {
    exact(catalog, ['ready', 'returnSupported', 'receiveModes', 'items']);
    if (typeof catalog.ready !== 'boolean' || typeof catalog.returnSupported !== 'boolean'
      || !Array.isArray(catalog.receiveModes) || catalog.receiveModes.length > 2
      || new Set(catalog.receiveModes).size !== catalog.receiveModes.length
      || catalog.receiveModes.some(m => !['single-unit', 'receive-all'].includes(m))
      || !Array.isArray(catalog.items) || catalog.items.length > 43 || new Set(catalog.items.map(i => i.item)).size !== catalog.items.length)
      throw new Error('INVALID_CATALOG');
    for (const item of catalog.items) {
      exact(item, ['item', 'available', 'maxPerRequest']);
      if (!itemName(item.item) || !Number.isSafeInteger(item.maxPerRequest) || item.maxPerRequest < 1 || item.maxPerRequest > 100
        || !Number.isSafeInteger(item.available) || item.available < 0 || item.available > item.maxPerRequest) throw new Error('INVALID_CATALOG');
    }
    const ready = catalog.ready && this.options.dispatchEnabled === true && this.options.whisperQualified === true;
    await this.request('/worker/v1/catalog', { gameServer: this.options.gameServer, ready,
      returnSupported: ready && catalog.returnSupported, receiveModes: catalog.receiveModes, items: catalog.items });
  }

  /** Call from the existing worker tick; this adapter never logs in or creates a game client. */
  async pollOnce(): Promise<void> {
    if (this.busy) return;
    this.busy = true;
    try {
      await this.reconcileProof();
      // An uncertain HTTP acknowledgement retries the same durable status,
      // including after restart. It never re-enters a game admission callback.
      const pending = this.options.ledger.operations().find(o => o.kind === 'ae_supply_operation' && o.data.publicationPending === true);
      if (pending) await this.publishRecord(pending);
      const reply = await this.request('/worker/v1/jobs/claim', {});
      exact(reply, ['job']);
      if (reply.job === null) return;
      const job = validateJob(reply.job, this.options.gameServer, this.now());
      const record = await this.options.withExclusiveOwner(() => this.admit(job));
      if (job.kind !== 'register-pairing') await this.publishRecord(record);
      await this.request(`/worker/v1/jobs/${job.id}/result`, { leaseToken: job.leaseToken, fence: job.fence,
        state: job.kind === 'register-pairing' ? (record.data.phase === 'rejected' ? 'rejected' : 'ready') : 'accepted' });
    } finally { this.busy = false; }
  }

  private async reconcileProof(): Promise<void> {
    const record = this.options.ledger.operations().find(o => o.kind === 'ae_pairing' && o.data.phase === 'proof-observed');
    if (!record) return;
    const reply = await this.request(`/worker/v1/pairings/${record.data.brokerId}/status`, {});
    exact(reply, ['id', 'character', 'gameServer', 'state']);
    if (reply.id !== record.data.brokerId || !sameCharacter(reply.character, record.data.payload.character)
      || reply.gameServer !== record.data.payload.gameServer) throw new Error('INVALID_STATUS');
    if (reply.state === 'ready' && Date.parse(record.data.payload.expiresAt) > this.now()) {
      await this.request(`/worker/v1/pairings/${record.data.brokerId}/proof`, { nonce: record.data.payload.nonce,
        character: record.data.payload.character, gameServer: record.data.payload.gameServer,
        observedAt: new Date(record.data.observedAt).toISOString() });
    } else if (reply.state !== 'observed') {
      if (reply.state !== 'expired') return;
      record.data.phase = 'rejected'; record.status = 'complete';
      await this.options.withExclusiveOwner(() => Promise.resolve(this.options.ledger.saveOperation(record)));
      return;
    }
    record.data.phase = 'proved';
    await this.options.withExclusiveOwner(() => Promise.resolve(this.options.ledger.saveOperation(record)));
  }

  private async admit(job: any): Promise<LedgerOperation> {
    const kind = job.kind === 'register-pairing' ? 'ae_pairing' : 'ae_supply_operation';
    let record = this.options.ledger.operations().find(o => o.kind === kind && o.data.brokerId === job.targetId);
    const intentHash = fingerprint(job.kind === 'register-pairing' ? job.payload : {
      ...job.payload, revoked: undefined, cancellationRequested: undefined,
      units: job.payload.units.map((unit: Unit) => ({ id: unit.id, item: unit.item })),
    });
    if (record) {
      if (record.data.intentHash !== intentHash && job.kind !== 'cancel-uncommitted' && job.kind !== 'reconcile-operation')
        throw new Error('INVALID_JOB');
      // A durable admission that might have reached a controller is reconciled, never re-executed.
      if (record.data.phase === 'running') {
        record.data.result = held(record.data.payload, (record.data.result?.sequence ?? 0) + 1);
        record.data.phase = 'review'; record.status = 'review';
        await this.options.ledger.saveOperation(record);
      }
      if (job.kind === 'cancel-uncommitted' || job.kind === 'reconcile-operation') {
        if (job.kind === 'cancel-uncommitted' || job.payload.revoked || job.payload.cancellationRequested) {
          record.data.dispatchBlocked = true;
          await this.options.ledger.saveOperation(record);
        }
        if (!this.options.reconcile) return record;
        const result = await this.options.reconcile({ ...record.data.payload, status: record.data.result,
          revoked: job.payload.revoked, cancellationRequested: job.payload.cancellationRequested });
        validateStatus(result, record.data.payload.units);
        record.data.result = result;
        await this.options.ledger.saveOperation(record);
      }
      return record;
    }
    if (job.kind === 'cancel-uncommitted' || job.kind === 'reconcile-operation') {
      // Never create a replacement game obligation from a cancellation or a client receipt report.
      record = await this.options.ledger.operation(kind, 'Exchanger', { brokerId: job.targetId, intentHash,
        payload: job.payload, phase: 'rejected', result: rejected(job.payload, 'RECONCILIATION_REQUIRED') });
      return record;
    }
    record = await this.options.ledger.operation(kind, 'Exchanger', { brokerId: job.targetId, intentHash,
      payload: job.payload, phase: job.kind === 'register-pairing' ? 'ready' : 'admitted' });
    if (job.kind === 'register-pairing') {
      if (!this.options.whisperQualified || Date.parse(job.payload.expiresAt) <= this.now()) {
        record.data.phase = 'rejected'; record.status = 'complete';
        await this.options.ledger.saveOperation(record);
      }
      return record;
    }
    const p = job.payload;
    const binding = this.options.ledger.operations().find(o => o.kind === 'ae_pairing' && o.data.brokerId === p.bindingId
      && o.data.phase === 'proved' && o.data.payload.installationId === p.installationId
      && sameCharacter(o.data.payload.character, p.character) && o.data.payload.gameServer === p.gameServer);
    let reason: string | undefined;
    if (!binding) reason = 'CHARACTER_NOT_ALLOWED';
    else if (!this.options.dispatchEnabled || p.revoked || p.cancellationRequested) reason = 'SERVICE_UNAVAILABLE';
    else if (Date.parse(p.credentialExpiresAt) <= this.now() || !Number.isSafeInteger(binding.data.observedAt)
      || binding.data.observedAt + 3600000 <= this.now()
      || Date.parse(p.credentialExpiresAt) > binding.data.observedAt + 3605000) reason = 'CREDENTIAL_EXPIRED';
    const callback = job.kind === 'reserve-supply' ? this.options.reserveSupply : this.options.registerReturn;
    if (!callback) reason = 'SERVICE_UNAVAILABLE';
    if (reason) {
      record.data.result = rejected(p, reason); record.data.phase = 'rejected'; record.status = 'complete';
      await this.options.ledger.saveOperation(record);
      return record;
    }
    record.data.phase = 'running';
    await this.options.ledger.saveOperation(record);
    try {
      const result = await callback!(structuredClone(p));
      validateStatus(result, p.units);
      record.data.result = result; record.data.phase = 'accepted';
    } catch {
      record.data.result = held(p, 1); record.data.phase = 'review'; record.status = 'review';
    }
    await this.options.ledger.saveOperation(record);
    return record;
  }

  /** Input must come directly from the qualified incoming game packet handler, never HTTP/client reports. */
  async observeWhisper(message: WhisperObservation): Promise<boolean> {
    if (!this.options.whisperQualified || message.channel !== 0 || message.outgoingEcho || message.recipient !== 'Exchanger'
      || message.gameServer !== this.options.gameServer || !Number.isSafeInteger(message.observedAt)
      || message.observedAt > this.now() + 5000 || message.observedAt < this.now() - 300000) return false;
    const match = /^"([A-Za-z]{1,30})"\s(AEQ-[A-Za-z0-9_-]{43})$/.exec(message.text);
    if (!match) return false;
    const record = await this.options.withExclusiveOwner(async () => {
      const found = this.options.ledger.operations().find(o => o.kind === 'ae_pairing' && o.data.phase === 'ready'
        && sameCharacter(o.data.payload.character, match[1]) && o.data.payload.nonce === match[2]
        && Date.parse(o.data.payload.expiresAt) > this.now() && message.observedAt >= o.createdAt);
      if (!found) return undefined;
      found.data.phase = 'proof-observed'; found.data.observedAt = message.observedAt;
      await this.options.ledger.saveOperation(found);
      return found;
    });
    if (!record) return false;
    await this.request(`/worker/v1/pairings/${record.data.brokerId}/proof`, { nonce: match[2], character: match[1],
      gameServer: message.gameServer, observedAt: new Date(message.observedAt).toISOString() });
    record.data.phase = 'proved';
    await this.options.withExclusiveOwner(() => Promise.resolve(this.options.ledger.saveOperation(record)));
    return true;
  }

  /** Existing controller receipt callbacks publish their durable canonical state here. */
  async publishOperation(operationId: string, status: OperationStatus): Promise<void> {
    if (!uuid(operationId)) throw new Error('INVALID_STATUS');
    const record = await this.options.withExclusiveOwner(async () => {
      const found = this.options.ledger.operations().find(o => o.kind === 'ae_supply_operation' && o.data.brokerId === operationId);
      if (!found) throw new Error('UNKNOWN_OPERATION');
      validateStatus(status, found.data.payload.units);
      if (status.sequence <= (found.data.result?.sequence ?? 0)) throw new Error('INVALID_STATUS');
      found.data.result = structuredClone(status);
      found.data.publicationPending = true;
      await this.options.ledger.saveOperation(found);
      return found;
    });
    await this.publishRecord(record);
  }
  private async publishRecord(record: LedgerOperation): Promise<void> {
    const current = await this.options.withExclusiveOwner(async () => {
      const found = this.options.ledger.operations().find(o => o.id === record.id);
      if (!found?.data.result) throw new Error('UNKNOWN_OPERATION');
      if (found.data.publicationPending !== true) {
        found.data.publicationPending = true;
        await this.options.ledger.saveOperation(found);
      }
      return found;
    });
    await this.request(`/worker/v1/operations/${current.data.brokerId}/status`, current.data.result);
    await this.options.withExclusiveOwner(async () => {
      const found = this.options.ledger.operations().find(o => o.id === current.id);
      // A newer controller receipt can arrive while HTTP is outstanding.
      if (found && fingerprint(found.data.result) === fingerprint(current.data.result) && found.data.publicationPending) {
        found.data.publicationPending = false;
        await this.options.ledger.saveOperation(found);
      }
    });
  }
}

export interface ParcelBridgeOptions {
  ledger: SupplyLedger; operationId: string; unitId: string; character: string; item: string;
  withExclusiveOwner(work: () => Promise<any>): Promise<any>;
  canStartOwnedParcel(): boolean;
  controller: { start(owner: string, recipient: string, item: string, kind: string, data: Record<string, unknown>): LedgerOperation | Promise<LedgerOperation> };
}
/** Uses the inspected ParcelSendController.start signature; it never invents packets or bulk quantities. */
export async function submitOneUnit(options: ParcelBridgeOptions): Promise<{ state: string; sourceAccepted: boolean; controllerOperationId?: string }> {
  if (!uuid(options.operationId) || !uuid(options.unitId) || !/^[A-Za-z]{1,30}$/.test(options.character)
    || !itemName(options.item)) throw new Error('INVALID_UNIT');
  return options.withExclusiveOwner(async () => {
    const accepted = options.ledger.operations().find(o => o.kind === 'ae_supply_operation' && o.data.brokerId === options.operationId
      && o.data.phase === 'accepted' && !o.data.dispatchBlocked && !o.data.payload.revoked && !o.data.payload.cancellationRequested
      && sameCharacter(o.data.payload.character, options.character)
      && o.data.payload.units.some((u: Unit) => u.id === options.unitId && u.item === options.item)
      && ['reserved', 'processing', 'partial'].includes(o.data.result?.state)
      && o.data.result.units.some((u: Unit) => u.id === options.unitId && !u.sourceAccepted && !u.cancelled));
    if (!accepted) throw new Error('UNIT_NOT_AUTHORIZED');
    if (options.ledger.operations().some(o => o.kind === 'ae_dispatch_unit' && o.data.unitId === options.unitId))
      return { state: 'reconcile', sourceAccepted: false };
    if (!options.canStartOwnedParcel()) return { state: 'held', sourceAccepted: false };
    const intent = await options.ledger.operation('ae_dispatch_unit', 'Exchanger', {
      brokerOperationId: options.operationId, unitId: options.unitId, character: options.character, item: options.item, submissionStarted: true,
    });
    // This acknowledged boundary precedes even controller construction/submission; an interrupted start is not retried.
    const controllerOperation = await options.controller.start('Exchanger', options.character, options.item, 'ae_supply_unit', {
      brokerOperationId: options.operationId, brokerUnitId: options.unitId,
    });
    intent.data.controllerOperationId = controllerOperation.id;
    await options.ledger.saveOperation(intent);
    return { state: 'submitted', sourceAccepted: false, controllerOperationId: controllerOperation.id };
  });
}

function validateJob(job: any, profile: string, now: number): any {
  exact(job, ['id', 'kind', 'targetId', 'fence', 'leaseToken', 'leaseExpiresAt', 'payload']);
  if (!uuid(job.id) || !uuid(job.targetId) || !Number.isSafeInteger(job.fence) || job.fence < 1
    || !/^[A-Za-z0-9_-]{43}$/.test(job.leaseToken) || !timestamp(job.leaseExpiresAt)
    || Date.parse(job.leaseExpiresAt) <= now || Date.parse(job.leaseExpiresAt) > now + 60000) throw new Error('INVALID_JOB');
  const p = job.payload;
  if (job.kind === 'register-pairing') {
    exact(p, ['id', 'installationId', 'clientOperationId', 'character', 'gameServer', 'verifierChallenge', 'nonce', 'receiver', 'purpose', 'expiresAt']);
    if (!uuid(p.installationId) || !uuid(p.clientOperationId) || !/^[A-Za-z0-9_-]{43}$/.test(p.verifierChallenge)
      || !/^AEQ-[A-Za-z0-9_-]{43}$/.test(p.nonce) || !timestamp(p.expiresAt)
      || !['quest-supplies', 'recovery'].includes(p.purpose)) throw new Error('INVALID_JOB');
  } else {
    if (!['reserve-supply', 'register-return', 'cancel-uncommitted', 'reconcile-operation'].includes(job.kind)) throw new Error('INVALID_JOB');
    exact(p, ['id', 'clientOperationId', 'journeyId', 'bindingId', 'installationId', 'character', 'gameServer', 'receiver', 'kind', 'manifest', 'units', 'credentialExpiresAt', 'revoked', 'cancellationRequested']);
    if (!uuid(p.clientOperationId) || !uuid(p.journeyId) || !uuid(p.bindingId) || !uuid(p.installationId) || !timestamp(p.credentialExpiresAt)
      || typeof p.revoked !== 'boolean' || typeof p.cancellationRequested !== 'boolean' || !['supply', 'return'].includes(p.kind)
      || job.kind === 'reserve-supply' && p.kind !== 'supply' || job.kind === 'register-return' && p.kind !== 'return'
      || !Array.isArray(p.units) || p.units.length < 1 || p.units.length > 100) throw new Error('INVALID_JOB');
    for (const unit of p.units) validateUnit(unit);
    if (new Set(p.units.map((u: Unit) => u.id)).size !== p.units.length) throw new Error('INVALID_JOB');
    const manifestFields = p.kind === 'supply' ? ['clientOperationId', 'journeyId', 'character', 'gameServer', 'quoteId', 'items']
      : ['clientOperationId', 'journeyId', 'character', 'gameServer', 'supplyRequestIds', 'items'];
    exact(p.manifest, manifestFields);
    if (p.manifest.clientOperationId !== p.clientOperationId || p.manifest.journeyId !== p.journeyId
      || !sameCharacter(p.manifest.character, p.character) || p.manifest.gameServer !== p.gameServer
      || p.kind === 'supply' && !uuid(p.manifest.quoteId)
      || p.kind === 'return' && (!Array.isArray(p.manifest.supplyRequestIds) || p.manifest.supplyRequestIds.length > 32 || !p.manifest.supplyRequestIds.every(uuid))
      || !Array.isArray(p.manifest.items) || p.manifest.items.length < 1 || p.manifest.items.length > 32) throw new Error('INVALID_JOB');
    const items = new Map<string, number>();
    for (const item of p.manifest.items) {
      exact(item, p.kind === 'supply' ? ['item', 'quantity'] : ['item', 'quantity', 'origin']);
      if (!itemName(item.item) || !Number.isSafeInteger(item.quantity) || item.quantity < 1 || item.quantity > 100 || items.has(item.item)
        || p.kind === 'return' && !['exchanger_supply', 'journey_surplus'].includes(item.origin)) throw new Error('INVALID_JOB');
      items.set(item.item, item.quantity);
    }
    if ([...items.values()].reduce((a, b) => a + b, 0) !== p.units.length
      || [...items].some(([item, count]) => p.units.filter((unit: Unit) => unit.item === item).length !== count)) throw new Error('INVALID_JOB');
  }
  if (p.id !== job.targetId || p.gameServer !== profile || p.receiver !== 'Exchanger' || !/^[A-Za-z]{1,30}$/.test(p.character)) throw new Error('INVALID_JOB');
  return structuredClone(job);
}
function validateUnit(unit: any): void {
  exact(unit, ['id', 'item', 'sourceAccepted', 'recipientObserved', 'cancelled', 'collected', 'banked', 'clientReported']);
  if (!uuid(unit.id) || !itemName(unit.item) || ['sourceAccepted', 'recipientObserved', 'cancelled', 'collected', 'banked', 'clientReported'].some(k => typeof unit[k] !== 'boolean')) throw new Error('INVALID_JOB');
}
function validateStatus(status: OperationStatus, units: Unit[]): void {
  exact(status, ['sequence', 'state', 'errorCode', 'units']);
  const states = ['reserved', 'processing', 'partial', 'sent', 'received', 'cancelled', 'rejected', 'review', 'registered', 'awaiting_player_send', 'awaiting_exchanger_receive', 'collecting', 'banking', 'complete'];
  const codes = ['QUOTE_CHANGED', 'RECONCILIATION_REQUIRED', 'STOCK_UNAVAILABLE', 'BUDGET_EXHAUSTED', 'CHARACTER_NOT_ALLOWED', 'UNSUPPORTED_ITEM', 'INVALID_QUANTITY', 'SERVICE_UNAVAILABLE', 'CANCELLED', 'CREDENTIAL_EXPIRED', 'REVOKED'];
  if (!Number.isSafeInteger(status.sequence) || status.sequence < 1 || !states.includes(status.state)
    || status.errorCode !== null && !codes.includes(status.errorCode) || !Array.isArray(status.units) || status.units.length !== units.length
    || new Set(status.units.map(u => u.id)).size !== units.length) throw new Error('INVALID_STATUS');
  for (const unit of status.units) {
    exact(unit, ['id', 'sourceAccepted', 'recipientObserved', 'cancelled', 'collected', 'banked']);
    if (!units.some(u => u.id === unit.id) || ['sourceAccepted', 'recipientObserved', 'cancelled', 'collected', 'banked'].some(k => typeof (unit as any)[k] !== 'boolean')) throw new Error('INVALID_STATUS');
  }
}
function statusUnits(p: any): OperationStatus['units'] { return p.units.map((u: Unit) => ({ id: u.id, sourceAccepted: u.sourceAccepted, recipientObserved: u.recipientObserved, cancelled: u.cancelled, collected: u.collected, banked: u.banked })); }
function rejected(p: any, code: string): OperationStatus { return { sequence: 1, state: 'rejected', errorCode: code, units: statusUnits(p) }; }
function held(p: any, sequence: number): OperationStatus { return { sequence, state: 'review', errorCode: 'RECONCILIATION_REQUIRED', units: statusUnits(p) }; }
function exact(value: any, keys: string[]): void {
  if (!value || typeof value !== 'object' || Array.isArray(value) || Object.keys(value).length !== keys.length
    || Object.keys(value).some(k => !keys.includes(k))) throw new Error('INVALID_JOB');
}
function uuid(value: unknown): value is string { return typeof value === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value) && value !== '00000000-0000-0000-0000-000000000000'; }
function itemName(value: unknown): value is string { return typeof value === 'string' && value.length > 0 && value.length <= 80 && !/[\x00-\x1f\x7f]/.test(value); }
function timestamp(value: unknown): value is string { return typeof value === 'string' && value.length <= 40 && Number.isFinite(Date.parse(value)); }
function sameCharacter(a: unknown, b: unknown): boolean { return typeof a === 'string' && typeof b === 'string' && a.toUpperCase() === b.toUpperCase(); }
function fingerprint(value: unknown): string {
  const canonical = (v: any): any => Array.isArray(v) ? v.map(canonical) : v && typeof v === 'object' ? Object.fromEntries(Object.keys(v).sort().map(k => [k, canonical(v[k])])) : v;
  return createHash('sha256').update(JSON.stringify(canonical(value))).digest('hex');
}
async function httpsRequest(origin: string, token: string, path: string, body: unknown): Promise<any> {
  if (!/^\/worker\/v1\/[A-Za-z0-9/-]+$/.test(path)) throw new Error('INVALID_ROUTE');
  const encoded = JSON.stringify(body);
  if (Buffer.byteLength(encoded, 'utf8') > 16384) throw new Error('REQUEST_TOO_LARGE');
  try {
    const response = await fetch(origin + path, { method: 'POST', redirect: 'error', signal: AbortSignal.timeout(10000),
      headers: { authorization: 'Bearer ' + token, 'content-type': 'application/json' }, body: encoded });
    if (!response.ok || !response.headers.get('content-type')?.startsWith('application/json') || !response.body) throw new Error();
    const chunks: Uint8Array[] = [];
    let size = 0;
    for await (const chunk of response.body) {
      size += chunk.length;
      if (size > 65536) throw new Error();
      chunks.push(chunk);
    }
    return JSON.parse(Buffer.concat(chunks).toString('utf8'));
  } catch { throw new Error('SERVICE_UNAVAILABLE'); }
}
