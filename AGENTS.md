# Efficient Testing in BovineLabs.Timeline.Physics

### 1. Direct Compilation Check
Validate syntax and references instantly without launching the Unity Editor:
```bash
dotnet build ../../BovineLabs.Timeline.Physics.Tests.csproj
```

### 2. Headless Test Execution
Run tests in batch mode for high-speed validation. Ensure no other Unity instances are using the project to prevent locks:
```bash
/home/i/Unity/Hub/Editor/6000.6.0a5/Editor/Unity \
    -runTests \
    -batchmode \
    -projectPath /home/i/GitHub/BovineLabs \
    -testResults TestResults_Physics.xml \
    -testFilter BovineLabs.Timeline.Physics.Tests \
    -testPlatform EditMode \
    -logFile -
```

### 3. Cleanup & Process Management
Identify and terminate lingering Unity processes to free project locks:
```bash
# Check for running instances
ps aux | grep Unity | grep -v grep

# Kill a specific process
kill <PID>
```

### 4. Rapid Result Verification
Quickly verify pass/fail status from the generated results XML:
```bash
grep -E "passed=|failed=" TestResults_Physics.xml | head -n 1
```
