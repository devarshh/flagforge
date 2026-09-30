// Creates or updates the pull request's single preview comment, found by its marker. Used by preview.yml through
// actions/github-script.
const marker = '<!-- flagforge-preview -->';

module.exports = async function upsertPreviewComment({ github, context }, text) {
  const { owner, repo } = context.repo;
  const issue_number = context.payload.pull_request.number;
  const body = `${marker}\n${text}`;
  const comments = await github.paginate(github.rest.issues.listComments, { owner, repo, issue_number, per_page: 100 });
  const existing = comments.find((comment) => comment.body && comment.body.includes(marker));
  if (existing) {
    await github.rest.issues.updateComment({ owner, repo, comment_id: existing.id, body });
  } else {
    await github.rest.issues.createComment({ owner, repo, issue_number, body });
  }
};
