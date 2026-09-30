## Summary

<!-- What changes, and why. Link the issue if there is one. -->

## How it was tested

<!-- Commands you ran, what you checked by hand, and anything you could not test. -->

## Screenshots

<!-- For dashboard or demo changes: before and after. Delete this section otherwise. -->

## Checklist

- [ ] Backend: `dotnet format --verify-no-changes`, build, and tests pass
- [ ] Frontend: `npm run lint`, `npm run typecheck`, `npm test`, and `npm run build` pass in `src/frontend`
- [ ] New behavior has tests
- [ ] Changes to the API contract are reflected in the dashboard, the SDK, and `docs/api.md`
- [ ] Kubernetes or infrastructure changes validate (`scripts/k8s-validate.sh`, `az bicep build --file infra/main.bicep`)
- [ ] No secrets, and no values that only work on one machine
- [ ] `PROGRESS.md` records any new decision
