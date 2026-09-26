import subprocess

# The exact-source transfer temporarily disables conversion for its checksum.
# Restore Windows checkout normalization so unrelated CRLF files do not appear
# as changes, especially workflows which this job never intends to modify.
subprocess.run(['git', 'config', 'core.autocrlf', 'true'], check=True)
subprocess.run(['git', 'restore', '--source=HEAD', '--worktree', '--', '.github/workflows'], check=True)
subprocess.run(['git', 'diff', '--exit-code', '--', '.github/workflows'], check=True)
print('Workflow files are unchanged; only application, tests and release notes will be recorded.')
