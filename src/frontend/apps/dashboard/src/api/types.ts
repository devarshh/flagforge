/** Types mirroring the management API's JSON (camelCase properties, camelCase enum values). */

export type Role = 'viewer' | 'editor' | 'admin';
export type FlagType = 'boolean' | 'string' | 'number' | 'json';
export type ClauseOperator =
  'in' | 'contains' | 'startsWith' | 'endsWith' | 'lt' | 'lte' | 'gt' | 'gte' | 'exists';
export type ScheduledChangeAction = 'turnOn' | 'turnOff' | 'setFallthrough';
export type ScheduledChangeStatus = 'pending' | 'processing' | 'completed' | 'failed' | 'cancelled';
export type ActorType = 'user' | 'system';
export type StaleReason = 'noRecentEvaluations' | 'fullyRolledOut';
export type ReasonKind =
  'OFF' | 'TARGET_MATCH' | 'RULE_MATCH' | 'FALLTHROUGH' | 'FLAG_NOT_FOUND' | 'ERROR';

export type JsonValue =
  string | number | boolean | null | JsonValue[] | { [key: string]: JsonValue };

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface User {
  id: string;
  email: string;
  displayName: string;
  role: Role;
  isActive: boolean;
  mustChangePassword: boolean;
  createdAt: string;
  lastLoginAt: string | null;
  lockoutEndsAt: string | null;
}

export interface UserRef {
  id: string;
  displayName: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  user: User;
}

export interface Meta {
  version: string;
  commit: string;
}

export interface Environment {
  id: string;
  key: string;
  name: string;
  color: string;
  isProtected: boolean;
  sortOrder: number;
  configVersion: number;
  createdAt: string;
}

export interface Project {
  id: string;
  key: string;
  name: string;
  description: string | null;
  createdAt: string;
  environments: Environment[];
}

export interface SdkKey {
  id: string;
  name: string;
  keyPrefix: string;
  createdAt: string;
  revokedAt: string | null;
}

export interface CreatedSdkKey {
  id: string;
  name: string;
  keyPrefix: string;
  createdAt: string;
  plaintextKey: string;
}

export interface Variation {
  id: string;
  name: string;
  value: JsonValue;
  description: string | null;
}

export interface Clause {
  attribute: string;
  operator: ClauseOperator;
  values: string[];
  negate: boolean;
}

export interface WeightedVariation {
  variationId: string;
  weight: number;
}

export interface Rollout {
  bucketBy: string;
  weights: WeightedVariation[];
}

export interface Serve {
  variationId?: string | null;
  rollout?: Rollout | null;
}

export interface Rule {
  id: string;
  description?: string | null;
  clauses: Clause[];
  serve: Serve;
}

export interface Target {
  variationId: string;
  contextKeys: string[];
}

export interface TargetingConfig {
  enabled: boolean;
  offVariationId: string;
  targets: Target[];
  rules: Rule[];
  fallthrough: Serve;
}

export interface Targeting extends TargetingConfig {
  environmentKey: string;
  version: number;
  updatedAt: string;
  updatedBy: UserRef | null;
}

export interface FlagEnvironmentSummary {
  environmentKey: string;
  enabled: boolean;
  version: number;
  lastEvaluatedAt: string | null;
}

export interface FlagSummary {
  id: string;
  key: string;
  name: string;
  description: string | null;
  type: FlagType;
  tags: string[];
  isPermanent: boolean;
  isArchived: boolean;
  isStale: boolean;
  createdAt: string;
  updatedAt: string;
  environments: FlagEnvironmentSummary[];
}

export interface FlagEnvironment {
  environmentKey: string;
  lastEvaluatedAt: string | null;
  config: Targeting;
}

export interface Flag {
  id: string;
  key: string;
  name: string;
  description: string | null;
  type: FlagType;
  variations: Variation[];
  tags: string[];
  isPermanent: boolean;
  isArchived: boolean;
  archivedAt: string | null;
  createdAt: string;
  updatedAt: string;
  environments: FlagEnvironment[];
}

export interface VariationInput {
  id?: string;
  name: string;
  value: JsonValue;
  description?: string | null;
}

export interface EvaluationReason {
  kind: ReasonKind;
  ruleId?: string;
  ruleIndex?: number;
  inRollout?: boolean;
}

export interface EvaluationResult {
  flagKey: string;
  value: JsonValue;
  variationId: string | null;
  reason: EvaluationReason;
}

export interface ScheduledChange {
  id: string;
  environmentKey: string;
  executeAt: string;
  action: ScheduledChangeAction;
  payload: Serve | null;
  status: ScheduledChangeStatus;
  attemptCount: number;
  executedAt: string | null;
  error: string | null;
  releasePlanId: string | null;
  createdAt: string;
  createdBy: UserRef;
}

export interface UsageBucket {
  bucketStart: string;
  variationId: string;
  count: number;
}

export interface StaleFlag {
  flagKey: string;
  name: string;
  reason: StaleReason;
  lastEvaluatedAt: string | null;
  servedVariationId: string | null;
}

export interface AuditEntry {
  id: number;
  occurredAt: string;
  actorType: ActorType;
  actorId: string | null;
  actorName: string;
  action: string;
  projectId: string | null;
  environmentId: string | null;
  flagId: string | null;
  resourceKey: string;
  comment: string | null;
  before: JsonValue | null;
  after: JsonValue | null;
}
