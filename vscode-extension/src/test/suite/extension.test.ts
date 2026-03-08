import * as assert from 'assert';
import * as vscode from 'vscode';

suite('Extension Test Suite', () => {
  void vscode.window.showInformationMessage('Start all tests.');

  test('Extension should be present', () => {
    assert.ok(
      vscode.extensions.getExtension(
        'light-query-profiler.light-query-profiler',
      ),
    );
  });

  test('Extension should activate', async () => {
    const extension = vscode.extensions.getExtension(
      'light-query-profiler.light-query-profiler',
    );
    assert.ok(extension);

    await extension.activate();
    assert.strictEqual(extension.isActive, true);
  });

  test('Show SQL Profiler command should be registered', async () => {
    const commands = await vscode.commands.getCommands(true);
    assert.ok(commands.includes('lightQueryProfiler.showProfiler'));
  });
});
