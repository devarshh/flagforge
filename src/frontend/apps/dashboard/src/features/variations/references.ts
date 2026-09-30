import type { Flag, Serve } from '../../api/types';

function serveReferences(serve: Serve, variationId: string): boolean {
  return (
    serve.variationId === variationId ||
    (serve.rollout?.weights.some((weight) => weight.variationId === variationId) ?? false)
  );
}

/** Names of the environments whose targeting uses a variation, with the server's rules for "in use". */
export function environmentsUsing(
  flag: Flag,
  environmentNames: ReadonlyMap<string, string>,
  variationId: string,
): string[] {
  return flag.environments
    .filter(
      ({ config }) =>
        config.offVariationId === variationId ||
        config.targets.some((target) => target.variationId === variationId) ||
        config.rules.some((rule) => serveReferences(rule.serve, variationId)) ||
        serveReferences(config.fallthrough, variationId),
    )
    .map(({ environmentKey }) => environmentNames.get(environmentKey) ?? environmentKey);
}
