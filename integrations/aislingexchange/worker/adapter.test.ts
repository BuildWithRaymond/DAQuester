import { test } from 'node:test';
import assert from 'node:assert/strict';
import { randomUUID, randomBytes, createHash } from 'node:crypto';
import { SupplyWorkerAdapter, submitOneUnit } from './adapter.ts';

class MemoryLedger {
  records: any[] = [];
  operations() { return structuredClone(this.records); }
  async operation(kind: string, owner: string, data: any) {
    const operation = { id: randomUUID(), kind, owner, data: structuredClone(data), status: 'pending', createdAt: Date.now() };
    this.records.push(operation);
    return structuredClone(operation);
  }
  async saveOperation(operation: any) {
    const index = this.records.findIndex(record => record.id === operation.id);
    if (index < 0) this.records.push(structuredClone(operation));
    else this.records[index] = structuredClone(operation);
  }
}

function fixture() {
  const ledger = new MemoryLedger();
  const sent: any[] = [];
  const jobs: any[] = [];
  let reservations = 0;
  let failReservation = false;
  const options = {
    origin: 'https://public.invalid', token: randomBytes(32).toString('base64url'), gameServer: 'synthetic-profile',
    whisperQualified: true, dispatchEnabled: true, ledger, now: () => Date.now(),
    withExclusiveOwner: async (work: () => Promise<unknown>) => await work(),
    reserveSupply: async (operation: any) => {
      reservations++;
      if (failReservation) throw new Error('Synthetic uncertain reservation');
      return { sequence: 1, state: 'reserved', errorCode: null, units: operation.units.map((u: any) => ({
        id: u.id, sourceAccepted: u.sourceAccepted, recipientObserved: u.recipientObserved, cancelled: u.cancelled, collected: u.collected, banked: u.banked,
      })) };
    },
    registerReturn: async () => { throw new Error('Unqualified synthetic return'); },
    reconcile: async (operation: any) => operation.status,
    request: async (path: string, body: any) => {
      sent.push({ path, body: structuredClone(body) });
      if (path === '/worker/v1/jobs/claim') return { job: jobs.shift() ?? null };
      if (path.endsWith('/proof')) return { state: 'observed' };
      return { state: 'acknowledged' };
    },
  };
  return { ledger, sent, jobs, options, reservations: () => reservations, failReservation: () => { failReservation = true; } };
}

function pairingJob() {
  const id = randomUUID();
  const payload = { id, installationId: randomUUID(), clientOperationId: randomUUID(), character: 'Alice', gameServer: 'synthetic-profile',
    verifierChallenge: randomBytes(32).toString('base64url'), nonce: 'AEQ-' + randomBytes(32).toString('base64url'),
    receiver: 'Exchanger', purpose: 'quest-supplies', expiresAt: new Date(Date.now() + 240000).toISOString() };
  return { id: randomUUID(), kind: 'register-pairing', targetId: id, fence: 1, leaseToken: randomBytes(32).toString('base64url'),
    leaseExpiresAt: new Date(Date.now() + 30000).toISOString(), payload };
}

function supplyJob(binding: any) {
  const id = randomUUID();
  const clientOperationId = randomUUID(), journeyId = randomUUID();
  return { id: randomUUID(), kind: 'reserve-supply', targetId: id, fence: 1, leaseToken: randomBytes(32).toString('base64url'),
    leaseExpiresAt: new Date(Date.now() + 30000).toISOString(), payload: {
      id, clientOperationId, journeyId, bindingId: binding.id, installationId: binding.installationId,
      character: 'Alice', gameServer: 'synthetic-profile', receiver: 'Exchanger', kind: 'supply', revoked: false, cancellationRequested: false,
      credentialExpiresAt: new Date(Date.now() + 3600000).toISOString(),
      manifest: { clientOperationId, journeyId, character: 'Alice', gameServer: 'synthetic-profile', quoteId: randomUUID(), items: [{ item: 'Mold', quantity: 1 }] },
      units: [{ id: randomUUID(), item: 'Mold', sourceAccepted: false, recipientObserved: false, cancelled: false, collected: false, banked: false, clientReported: false }],
    } };
}

async function prove(adapter: SupplyWorkerAdapter, f: ReturnType<typeof fixture>, job: any) {
  f.jobs.push(job);
  await adapter.pollOnce();
  const observed = await adapter.observeWhisper({ channel: 0, text: '"Alice" ' + job.payload.nonce,
    outgoingEcho: false, gameServer: 'synthetic-profile', recipient: 'Exchanger', observedAt: Date.now() });
  assert.equal(observed, true);
}

test('adapter refuses insecure origins and starts without creating a game client', () => {
  const f = fixture();
  assert.throws(() => new SupplyWorkerAdapter({ ...f.options, origin: 'http://public.invalid' }));
  assert.throws(() => new SupplyWorkerAdapter({ ...f.options, origin: 'https://public.invalid/private/path' }));
});

test('lost status response retries durable canonical receipt after restart without dispatch replay', async () => {
  const f = fixture();
  let lose = true;
  const options = { ...f.options, request: async (path: string, body: any) => {
    if (path.endsWith('/status') && body.state === 'sent' && lose) {
      lose = false;
      throw new Error('Synthetic lost response');
    }
    return f.options.request(path, body);
  } };
  const adapter = new SupplyWorkerAdapter(options), binding = pairingJob();
  await prove(adapter, f, binding);
  const job = supplyJob(binding.payload); f.jobs.push(job); await adapter.pollOnce();
  const status = { sequence: 2, state: 'sent', errorCode: null, units: job.payload.units.map((u: any) => ({
    id: u.id, sourceAccepted: true, recipientObserved: false, cancelled: false, collected: false, banked: false,
  })) };
  await assert.rejects(adapter.publishOperation(job.targetId, status), /lost response/);
  assert.equal(f.ledger.records.find(o => o.kind === 'ae_supply_operation').data.publicationPending, true);
  await new SupplyWorkerAdapter(options).pollOnce();
  assert.deepEqual(f.sent.find(s => s.body.state === 'sent')?.body, status);
  assert.equal(f.ledger.records.find(o => o.kind === 'ae_supply_operation').data.publicationPending, false);
  assert.equal(f.reservations(), 1);
});

test('catalog publication cannot enable dispatch when local qualification is off', async () => {
  const f = fixture();
  const adapter = new SupplyWorkerAdapter({ ...f.options, dispatchEnabled: false });
  assert.equal(typeof (adapter as any).publishCatalog, 'function');
  await (adapter as any).publishCatalog({ ready: true, returnSupported: true, receiveModes: ['single-unit'],
    items: [{ item: 'Mold', available: 3, maxPerRequest: 3 }] });
  const published = f.sent.find(s => s.path === '/worker/v1/catalog');
  assert.equal(published.body.ready, false);
  assert.equal(published.body.returnSupported, false);
  assert.equal(published.body.gameServer, 'synthetic-profile');
});

test('unrecognized command fields never reach a controller callback', async () => {
  const f = fixture();
  const job: any = pairingJob();
  job.payload.command = 'arbitrary';
  f.jobs.push(job);
  const adapter = new SupplyWorkerAdapter(f.options);
  await assert.rejects(adapter.pollOnce(), { message: 'INVALID_JOB' });
  assert.equal(f.reservations(), 0);
  assert.equal(f.ledger.records.length, 0);
});

test('only exact non-echo direct whispers prove pairing once', async () => {
  const f = fixture();
  const adapter = new SupplyWorkerAdapter(f.options);
  const job = pairingJob();
  f.jobs.push(job);
  await adapter.pollOnce();
  for (const change of [{ channel: 3 }, { outgoingEcho: true }, { text: '"Mallory" ' + job.payload.nonce }, { recipient: 'Other' }]) {
    assert.equal(await adapter.observeWhisper({ channel: 0, text: '"Alice" ' + job.payload.nonce, outgoingEcho: false,
      gameServer: 'synthetic-profile', recipient: 'Exchanger', observedAt: Date.now(), ...change }), false);
  }
  const message = { channel: 0, text: '"Alice" ' + job.payload.nonce, outgoingEcho: false,
    gameServer: 'synthetic-profile', recipient: 'Exchanger', observedAt: Date.now() };
  assert.equal(await adapter.observeWhisper(message), true);
  assert.equal(await adapter.observeWhisper(message), false);
  assert.equal(f.sent.filter(s => s.path.endsWith('/proof')).length, 1);
});

test('supply requires a binding independently observed by private worker', async () => {
  const f = fixture();
  f.jobs.push(supplyJob(pairingJob().payload));
  const adapter = new SupplyWorkerAdapter(f.options);
  await adapter.pollOnce();
  assert.equal(f.reservations(), 0);
  assert.equal(f.sent.some(s => s.body.state === 'rejected'), true);
});

test('redelivery after uncertain reservation never calls reservation again', async () => {
  const f = fixture();
  const adapter = new SupplyWorkerAdapter(f.options);
  const binding = pairingJob();
  await prove(adapter, f, binding);
  const job = supplyJob(binding.payload);
  f.failReservation();
  f.jobs.push(job);
  await adapter.pollOnce();
  f.jobs.push({ ...job, fence: 2, leaseToken: randomBytes(32).toString('base64url') });
  await adapter.pollOnce();
  assert.equal(f.reservations(), 1);
  assert.equal(f.sent.some(s => s.body.state === 'review'), true);
});

test('valid private binding admits exactly one reservation across redelivery', async () => {
  const f = fixture();
  const adapter = new SupplyWorkerAdapter(f.options);
  const binding = pairingJob();
  await prove(adapter, f, binding);
  const job = supplyJob(binding.payload);
  f.jobs.push(job);
  await adapter.pollOnce();
  f.jobs.push({ ...job, fence: 2, leaseToken: randomBytes(32).toString('base64url') });
  await adapter.pollOnce();
  assert.equal(f.reservations(), 1);
  assert.equal(f.sent.filter(s => s.path.endsWith('/status') && s.body.state === 'reserved').length, 2);
});

test('settled source dispatch permits a second request without a client delivery claim', async () => {
  const f = fixture();
  const adapter = new SupplyWorkerAdapter(f.options), binding = pairingJob();
  await prove(adapter, f, binding);
  const first = supplyJob(binding.payload);
  f.jobs.push(first);
  await adapter.pollOnce();
  const sent = { sequence: 2, state: 'sent', errorCode: null, units: first.payload.units.map(unit => ({
    id: unit.id, sourceAccepted: true, recipientObserved: false, cancelled: false, collected: false, banked: false,
  })) };
  await adapter.publishOperation(first.targetId, sent);
  const second = supplyJob(binding.payload);
  f.jobs.push(second);
  await adapter.pollOnce();
  assert.equal(f.reservations(), 2);
  const previous = f.ledger.operations().find(operation => operation.data.brokerId === first.targetId);
  assert.deepEqual(previous.data.result, sent);
  assert.equal(previous.data.result.units[0].recipientObserved, false);
});

test('late forged client flags and reconcile redelivery cannot create new reservation or trusted receipts', async () => {
  const f = fixture();
  const adapter = new SupplyWorkerAdapter(f.options), binding = pairingJob();
  await prove(adapter, f, binding);
  const first = supplyJob(binding.payload);
  f.jobs.push(first);
  await adapter.pollOnce();
  const sent = { sequence: 2, state: 'sent', errorCode: null, units: first.payload.units.map(unit => ({
    id: unit.id, sourceAccepted: true, recipientObserved: false, cancelled: false, collected: false, banked: false,
  })) };
  await adapter.publishOperation(first.targetId, sent);
  const report = { ...first, id: randomUUID(), kind: 'reconcile-operation', payload: {
    ...first.payload, units: first.payload.units.map(unit => ({ ...unit, sourceAccepted: true,
      clientReported: true, recipientObserved: true, collected: true, banked: true })),
  } };
  f.jobs.push(report);
  await adapter.pollOnce();
  f.jobs.push({ ...report, id: randomUUID(), fence: 2 });
  await new SupplyWorkerAdapter(f.options).pollOnce();
  assert.equal(f.reservations(), 1);
  const previous = f.ledger.operations().find(operation => operation.data.brokerId === first.targetId);
  assert.deepEqual(previous.data.result, sent);
  assert.equal(previous.data.payload.units[0].clientReported, false);
  assert.equal(f.sent.filter(request => request.body.state === 'sent').every(request =>
    request.body.units.every((unit: any) => !unit.recipientObserved && !unit.collected && !unit.banked)), true);
});

test('lost proof response reconciles trusted observation without another game whisper', async () => {
  const f = fixture();
  const binding = pairingJob();
  const original = f.options.request;
  f.options.request = async (path: string, body: any) => {
    if (path.endsWith('/proof')) throw new Error('SERVICE_UNAVAILABLE');
    if (path === `/worker/v1/pairings/${binding.targetId}/status`) return {
      id: binding.targetId, character: 'Alice', gameServer: 'synthetic-profile', state: 'observed',
    };
    return original(path, body);
  };
  const adapter = new SupplyWorkerAdapter(f.options);
  f.jobs.push(binding);
  await adapter.pollOnce();
  await assert.rejects(adapter.observeWhisper({ channel: 0, text: '"Alice" ' + binding.payload.nonce, outgoingEcho: false,
    gameServer: 'synthetic-profile', recipient: 'Exchanger', observedAt: Date.now() }), { message: 'SERVICE_UNAVAILABLE' });
  f.jobs.push(supplyJob(binding.payload));
  await adapter.pollOnce();
  assert.equal(f.reservations(), 1);
});

test('broker cannot extend a private character proof beyond one hour', async () => {
  const f = fixture();
  const adapter = new SupplyWorkerAdapter(f.options);
  const binding = pairingJob();
  await prove(adapter, f, binding);
  const job = supplyJob(binding.payload);
  job.payload.credentialExpiresAt = new Date(Date.now() + 7200000).toISOString();
  f.jobs.push(job);
  await adapter.pollOnce();
  assert.equal(f.reservations(), 0);
  assert.equal(f.sent.some(s => s.body.errorCode === 'CREDENTIAL_EXPIRED'), true);
});

test('cancellation blocks unsubmitted parcels even without a reconciliation callback', async () => {
  const f = fixture();
  const adapter = new SupplyWorkerAdapter({ ...f.options, reconcile: undefined });
  const binding = pairingJob();
  await prove(adapter, f, binding);
  const job = supplyJob(binding.payload);
  f.jobs.push(job);
  await adapter.pollOnce();
  f.jobs.push({ ...job, id: randomUUID(), kind: 'cancel-uncommitted', payload: { ...job.payload, cancellationRequested: true } });
  await adapter.pollOnce();
  await assert.rejects(submitOneUnit({ ledger: f.ledger, operationId: job.targetId, unitId: job.payload.units[0].id,
    character: 'Alice', item: 'Mold', withExclusiveOwner: async work => await work(), canStartOwnedParcel: () => true,
    controller: { start: async () => { throw new Error('must not start'); } },
  }), { message: 'UNIT_NOT_AUTHORIZED' });
});

test('parcel bridge refuses units absent from the authoritative accepted manifest', async () => {
  const ledger = new MemoryLedger();
  let starts = 0;
  await assert.rejects(submitOneUnit({ ledger, operationId: randomUUID(), unitId: randomUUID(), character: 'Alice', item: 'Mold',
    withExclusiveOwner: async work => await work(), canStartOwnedParcel: () => true,
    controller: { start: async () => { starts++; return {} as any; } },
  }), { message: 'UNIT_NOT_AUTHORIZED' });
  assert.equal(starts, 0);
});

test('parcel bridge persists a no-replay boundary before existing controller start', async () => {
  const ledger = new MemoryLedger();
  let starts = 0;
  const options = {
    ledger, operationId: randomUUID(), unitId: randomUUID(), character: 'Alice', item: 'Mold',
    withExclusiveOwner: async (work: () => Promise<unknown>) => await work(),
    canStartOwnedParcel: () => true,
    controller: { start: async (owner: string, recipient: string, item: string, kind: string, data: any) => {
      starts++;
      assert.equal(owner, 'Exchanger');
      assert.equal(recipient, 'Alice');
      assert.equal(ledger.operations().some(o => o.data.unitId === options.unitId && o.data.submissionStarted === true), true);
      return { id: randomUUID(), kind, owner, data: { ...data, sourceAccepted: false }, status: 'pending', createdAt: Date.now() };
    } },
  };
  await ledger.operation('ae_supply_operation', 'Exchanger', { brokerId: options.operationId, phase: 'accepted',
    payload: { character: 'Alice', units: [{ id: options.unitId, item: 'Mold' }] },
    result: { state: 'reserved', units: [{ id: options.unitId, sourceAccepted: false, cancelled: false }] },
  });
  const first = await submitOneUnit(options);
  assert.equal(first.sourceAccepted, false);
  const again = await submitOneUnit(options);
  assert.equal(again.state, 'reconcile');
  assert.equal(starts, 1);
});
