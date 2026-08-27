# Correction submission workflow

The corrections page is a static Hugo page hosted on GitHub Pages. It sends submissions to Formspree because browser code cannot safely hold the credentials needed to call the GitHub Issues API.

The endpoint is configured as `params.forms.correctionAction` in `src/config/_default/params.toml`. It currently uses the site's existing Formspree inbox so the form works immediately. The `subject` and `tags` fields keep correction reports easy to identify and triage.

## Recommended moderation workflow

Keep Formspree as the private moderation inbox. Review each submission there, correct the site directly when the change is straightforward, and create a GitHub issue manually when the work needs tracking. This prevents spam, personal contact details, and abusive content from being published automatically in the public repository.

Formspree supplies service-side spam filtering and rate limiting. The form also includes the supported `_gotcha` honeypot, browser validation, length limits, and duplicate-submission protection.

In the Formspree project settings, restrict submissions to the production domain. CAPTCHA can also be enabled there if spam becomes a problem.

## Optional automatic GitHub issues

Formspree has a GitHub Workflow integration that can create an issue without exposing a GitHub token in the website. To use it:

1. Create a dedicated corrections form in Formspree.
2. Replace `params.forms.correctionAction` with that form's endpoint.
3. Ensure the `correction`, `content`, and `triage` labels exist in the repository.
4. In the form's **Workflow** view, add the GitHub action and select this repository.
5. Submit and review a test correction before enabling it for visitors.

Do not connect the shared contact endpoint to the GitHub action: that would also turn ordinary contact messages into issues. Automatic creation is also unsuitable for a public repository while the form collects optional names and email addresses, because submitted fields may become public. Keep the moderated workflow unless the destination repository is private or contact fields are removed from the automated form.

See the [Formspree GitHub integration documentation](https://help.formspree.io/articles/plugins/use-github-to-add-issues-to-a-repository) for the dashboard steps.
