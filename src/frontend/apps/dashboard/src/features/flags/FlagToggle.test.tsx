import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { queryKeys } from '../../api/queryKeys';
import type { Environment, FlagSummary, PagedResult, Role } from '../../api/types';
import { jsonResponse, renderWithProviders, stubFetch, testUser } from '../../test/render';
import { FlagToggle } from './FlagToggle';

const development: Environment = {
  id: 'e1',
  key: 'development',
  name: 'Development',
  color: '#3A7CA5',
  isProtected: false,
  sortOrder: 0,
  configVersion: 1,
  createdAt: '2026-09-01T00:00:00Z',
};

const production: Environment = {
  ...development,
  id: 'e3',
  key: 'production',
  name: 'Production',
  color: '#C2362B',
  isProtected: true,
  sortOrder: 2,
};

function renderToggle(role: Role, environment: Environment, archived = false) {
  return renderWithProviders(
    <FlagToggle
      projectKey="acme"
      flagKey="new-checkout"
      environment={environment}
      enabled={false}
      archived={archived}
    />,
    { user: testUser(role) },
  );
}

function lamp(environment: Environment) {
  return screen.getByRole('switch', { name: `new-checkout in ${environment.name}` });
}

describe('FlagToggle', () => {
  const environments = { Development: development, Production: production };

  it.each<[Role, keyof typeof environments, string | null]>([
    ['viewer', 'Development', 'Viewers cannot change flags. Ask an admin for the Editor role.'],
    ['viewer', 'Production', 'Viewers cannot change flags. Ask an admin for the Editor role.'],
    ['editor', 'Development', null],
    ['editor', 'Production', 'Production is protected. Only admins can change it.'],
    ['admin', 'Development', null],
    ['admin', 'Production', null],
  ])('%s in %s: disabled reason %j', async (role, environmentName, reason) => {
    const user = userEvent.setup();
    const environment = environments[environmentName];
    renderToggle(role, environment);

    if (reason === null) {
      expect(lamp(environment)).toBeEnabled();
      return;
    }

    expect(lamp(environment)).toBeDisabled();
    await user.hover(lamp(environment).parentElement!);
    expect(await screen.findByRole('tooltip')).toHaveTextContent(reason);
  });

  it('is disabled for archived flags, even for admins', () => {
    renderToggle('admin', development, true);
    expect(lamp(development)).toBeDisabled();
  });

  it('turns a flag on at once in an unprotected environment, updating cached lists optimistically', async () => {
    const user = userEvent.setup();
    let respond: (response: Response) => void = () => undefined;
    const { requests } = stubFetch(() => new Promise<Response>((resolve) => (respond = resolve)));
    const listKey = queryKeys.flagList('acme', {
      search: '',
      tag: null,
      includeArchived: false,
      page: 1,
      pageSize: 25,
    });
    const page: PagedResult<FlagSummary> = {
      items: [
        {
          id: 'f1',
          key: 'new-checkout',
          name: 'New checkout',
          description: null,
          type: 'boolean',
          tags: [],
          isPermanent: false,
          isArchived: false,
          isStale: false,
          createdAt: '2026-09-01T00:00:00Z',
          updatedAt: '2026-09-01T00:00:00Z',
          environments: [
            { environmentKey: 'development', enabled: false, version: 3, lastEvaluatedAt: null },
          ],
        },
      ],
      page: 1,
      pageSize: 25,
      totalCount: 1,
    };
    const { queryClient } = renderToggle('editor', development);
    queryClient.setQueryData(listKey, page);

    await user.click(lamp(development));

    await waitFor(() => expect(requests).toHaveLength(1));
    expect(requests[0]).toMatchObject({
      method: 'POST',
      url: '/api/v1/projects/acme/flags/new-checkout/environments/development/toggle',
      body: { enabled: true },
    });
    expect(
      queryClient.getQueryData<PagedResult<FlagSummary>>(listKey)?.items[0]?.environments[0]
        ?.enabled,
    ).toBe(true);

    respond(
      jsonResponse(
        { title: 'Forbidden', status: 403, detail: 'Development is locked for maintenance.' },
        403,
      ),
    );

    expect(await screen.findByText('Development is locked for maintenance.')).toBeInTheDocument();
    expect(
      queryClient.getQueryData<PagedResult<FlagSummary>>(listKey)?.items[0]?.environments[0]
        ?.enabled,
    ).toBe(false);
  });

  it('asks for the flag key and a comment in a protected environment', async () => {
    const user = userEvent.setup();
    const { requests } = stubFetch(() =>
      jsonResponse({
        environmentKey: 'production',
        enabled: true,
        offVariationId: 'false',
        targets: [],
        rules: [],
        fallthrough: { variationId: 'true', rollout: null },
        version: 4,
        updatedAt: '2026-09-30T10:00:00Z',
        updatedBy: null,
      }),
    );
    renderToggle('admin', production);

    await user.click(lamp(production));
    const dialog = await screen.findByRole('dialog', {
      name: 'Turn on new-checkout in Production?',
    });
    const confirm = screen.getByRole('button', { name: 'Turn on' });
    expect(requests).toHaveLength(0);
    expect(confirm).toBeDisabled();

    await user.type(
      screen.getByRole('textbox', { name: 'Type new-checkout to confirm' }),
      'new-checkout',
    );
    expect(confirm).toBeDisabled();
    await user.type(screen.getByRole('textbox', { name: /Comment/ }), 'Launch day');
    await user.click(confirm);

    await waitFor(() => expect(dialog).not.toBeInTheDocument());
    expect(requests[0]).toMatchObject({
      method: 'POST',
      url: '/api/v1/projects/acme/flags/new-checkout/environments/production/toggle',
      body: { enabled: true, comment: 'Launch day' },
    });
    expect(await screen.findByText('Flag turned on in Production')).toBeInTheDocument();
  });
});
