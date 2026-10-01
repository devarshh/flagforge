# 0007. Ephemeral per-PR preview environments with in-namespace SQL Server

## Context

Reviewers should be able to try a pull request in a real deployment, and the full smoke test should run against it
before merge. Previews must not touch production data and must clean up after themselves.

## Decision

Each pull request from this repository gets the namespace `ff-pr-<number>` with its own SQL Server StatefulSet (the
`ephemeral-sql` component, data in an `emptyDir`), images tagged `pr-<number>-<commit>`, and the hostname
`pr-<number>.<gateway ip>.nip.io` on the shared Gateway. The workflow generates the SA password and signing key on the
first deploy and keeps them afterwards, runs the migrator, waits for rollouts, runs the full smoke test, and keeps one
sticky comment on the pull request up to date. Closing the pull request deletes the namespace and its images; a daily
janitor removes previews whose pull request is closed or merged.

## Consequences

- Each preview is isolated and disposable; production is never involved.
- SQL Server needs about 2 GB of memory per preview, so the number of open previews is limited by the cluster's size
  (the autoscaler adds nodes up to its maximum). Lower CPU requests help them fit.
- Data does not survive a SQL Server restart, which is fine for a preview and is reseeded by the migrator.
- Forks get no preview, because they cannot use the repository's Azure credentials.
