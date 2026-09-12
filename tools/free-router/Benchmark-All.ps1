$ErrorActionPreference = 'Stop'
& python -B (Join-Path $PSScriptRoot 'benchmark.py') --models all
if ($LASTEXITCODE -ne 0) { throw 'Benchmark di base fallito.' }
& python -B (Join-Path $PSScriptRoot 'benchmark_workflow.py')
if ($LASTEXITCODE -ne 0) { throw 'Benchmark operativo fallito.' }
