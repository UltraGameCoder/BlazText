# Working in this repository

Read [CONTRIBUTING.md](CONTRIBUTING.md) before changing anything — it holds the
authoritative checklists. This file exists because that one is easy to skip.
Everything below is a rule those checklists already imply but that is repeatedly
missed in practice.

## Docs are part of the contract

`docs/` states behaviour that users rely on. Treat a sentence there like a test
that has not been written yet.

- **Read the relevant `docs/` page before changing a feature's behaviour**, not
  after. A change that contradicts a documented promise is a bug even when every
  test passes.
- When a doc makes a behavioural promise, back it with a test whose name says the
  same thing. Prose cannot defend itself.
- Update the page in the same PR as the change (CONTRIBUTING step 4).
- Breaking changes should not reshape the docs. If a change forces a page to be
  rewritten rather than adjusted, treat that as a signal the change may be wrong —
  proceed only when it fixes a genuine problem, and record it in
  [CHANGELOG.md](CHANGELOG.md).

## Comments

- Keep inline comments minimal. Write one only when the intent is not already
  clear from the code. A comment restating the next line is noise.
- **A comment on a method, property, or type is API documentation** — it renders
  in other developers' IDEs. Describe what the member does and how to use it.
  Never put the history of the implementation there; that belongs in the commit
  message or the pull request.

## Verifying

- `dotnet build BlazText.slnx` and `dotnet test` must pass.
- JS-dependent behaviour (caret handling, keyboard interception, search
  highlighting, paste) is not covered by bUnit. Run the demo app against
  CONTRIBUTING's *Manual verification checklist* and say in the PR what you
  actually checked.
- Claims in a PR description must be things you ran, not things you expect.
