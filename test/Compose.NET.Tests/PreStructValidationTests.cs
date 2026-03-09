// Copyright 2026 jx
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Xunit;
using Compose.NET.Errors;
using Compose.NET.Loader;

namespace Compose.NET.Tests;

/// <summary>
/// Tests for pre-struct validations in SchemaValidator.ValidateLogic.
/// These mirror compose-go's validation.Validate() pre-struct checks.
/// </summary>
public class PreStructValidationTests
{
    private static LoadResult Load(string yaml) =>
        ComposeLoader.LoadFromContent(yaml);

    private static ComposeInvalidException ExpectValidationError(string yaml) =>
        Assert.Throws<ComposeInvalidException>(() => ComposeLoader.LoadFromContent(yaml));

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // 1. Port host_ip IP validation
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void PortHostIp_ValidIPv4_NoWarning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    ports:
      - host_ip: 127.0.0.1
        target: 80
        published: ""8080""
";
        var result = Load(yaml);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("host_ip"));
    }

    [Fact]
    public void PortHostIp_ValidIPv6_NoWarning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    ports:
      - host_ip: ""::1""
        target: 80
        published: ""8080""
";
        var result = Load(yaml);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("host_ip"));
    }

    [Fact]
    public void PortHostIp_InvalidIP_Warning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    ports:
      - host_ip: not-an-ip
        target: 80
        published: ""8080""
";
        var ex = ExpectValidationError(yaml);
        Assert.Contains("host_ip", ex.Message);
        Assert.Contains("not a valid IP address", ex.Message);
    }

    [Fact]
    public void PortHostIp_Empty_NoWarning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    ports:
      - host_ip: """"
        target: 80
        published: ""8080""
";
        var result = Load(yaml);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("host_ip") && w.Contains("not a valid IP address"));
    }

    [Fact]
    public void PortHostIp_ZeroAddress_NoWarning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    ports:
      - host_ip: 0.0.0.0
        target: 80
        published: ""8080""
";
        var result = Load(yaml);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("host_ip") && w.Contains("not a valid IP address"));
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // 2. External resource conflict check
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void ExternalVolume_OnlyNameAllowed_NoWarning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    volumes:
      - mydata:/data
volumes:
  mydata:
    external: true
    name: real-volume-name
";
        var result = Load(yaml);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("external") && w.Contains("not allowed"));
    }

    [Fact]
    public void ExternalVolume_WithDriver_Warning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    volumes:
      - mydata:/data
volumes:
  mydata:
    external: true
    driver: local
";
        var ex = ExpectValidationError(yaml);
        Assert.Contains("volumes.mydata", ex.Message);
        Assert.Contains("driver", ex.Message);
        Assert.Contains("not allowed", ex.Message);
        Assert.Contains("external", ex.Message);
    }

    [Fact]
    public void ExternalNetwork_WithDriver_Warning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    networks:
      - mynet
networks:
  mynet:
    external: true
    driver: overlay
";
        var ex = ExpectValidationError(yaml);
        Assert.Contains("networks.mynet", ex.Message);
        Assert.Contains("driver", ex.Message);
        Assert.Contains("not allowed", ex.Message);
    }

    [Fact]
    public void ExternalSecret_WithFile_Warning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    secrets:
      - mysecret
secrets:
  mysecret:
    external: true
    file: ./secret.txt
";
        var ex = ExpectValidationError(yaml);
        Assert.Contains("secrets.mysecret", ex.Message);
        Assert.Contains("file", ex.Message);
        Assert.Contains("not allowed", ex.Message);
    }

    [Fact]
    public void ExternalConfig_WithContent_Warning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    configs:
      - myconfig
configs:
  myconfig:
    external: true
    content: ""hello""
";
        var ex = ExpectValidationError(yaml);
        Assert.Contains("configs.myconfig", ex.Message);
        Assert.Contains("content", ex.Message);
        Assert.Contains("not allowed", ex.Message);
    }

    [Fact]
    public void ExternalVolume_WithExtensionKey_NoWarning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    volumes:
      - mydata:/data
volumes:
  mydata:
    external: true
    x-custom: value
";
        var result = Load(yaml);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("not allowed"));
    }

    [Fact]
    public void NonExternalVolume_WithDriver_NoWarning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    volumes:
      - mydata:/data
volumes:
  mydata:
    driver: local
";
        var result = Load(yaml);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("not allowed") && w.Contains("external"));
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // 3. Configs file/environment/content mutual exclusivity
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Config_FileAndEnvironment_Warning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    configs:
      - myconfig
configs:
  myconfig:
    file: ./config.txt
    environment: MY_CONFIG_VAR
";
        var ex = ExpectValidationError(yaml);
        Assert.Contains("configs.myconfig", ex.Message);
        Assert.Contains("mutually exclusive", ex.Message);
    }

    [Fact]
    public void Config_FileAndContent_Warning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    configs:
      - myconfig
configs:
  myconfig:
    file: ./config.txt
    content: ""hello""
";
        var ex = ExpectValidationError(yaml);
        Assert.Contains("configs.myconfig", ex.Message);
        Assert.Contains("mutually exclusive", ex.Message);
    }

    [Fact]
    public void Config_EnvironmentAndContent_Warning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    configs:
      - myconfig
configs:
  myconfig:
    environment: MY_VAR
    content: ""hello""
";
        var ex = ExpectValidationError(yaml);
        Assert.Contains("configs.myconfig", ex.Message);
        Assert.Contains("mutually exclusive", ex.Message);
    }

    [Fact]
    public void Config_AllThree_Warning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    configs:
      - myconfig
configs:
  myconfig:
    file: ./config.txt
    environment: MY_VAR
    content: ""hello""
";
        var ex = ExpectValidationError(yaml);
        Assert.Contains("configs.myconfig", ex.Message);
        Assert.Contains("mutually exclusive", ex.Message);
    }

    [Fact]
    public void Config_OnlyFile_NoWarning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    configs:
      - myconfig
configs:
  myconfig:
    file: ./config.txt
";
        var result = Load(yaml);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("mutually exclusive") && w.Contains("configs"));
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // 4. Secrets file/environment mutual exclusivity
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void Secret_FileAndEnvironment_Warning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    secrets:
      - mysecret
secrets:
  mysecret:
    file: ./secret.txt
    environment: MY_SECRET_VAR
";
        var ex = ExpectValidationError(yaml);
        Assert.Contains("secrets.mysecret", ex.Message);
        Assert.Contains("mutually exclusive", ex.Message);
    }

    [Fact]
    public void Secret_OnlyFile_NoMutualExclusivityWarning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    secrets:
      - mysecret
secrets:
  mysecret:
    file: ./secret.txt
";
        var result = Load(yaml);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("mutually exclusive") && w.Contains("secrets"));
    }

    [Fact]
    public void Secret_OnlyEnvironment_NoMutualExclusivityWarning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    secrets:
      - mysecret
secrets:
  mysecret:
    environment: MY_SECRET_VAR
";
        var result = Load(yaml);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("mutually exclusive") && w.Contains("secrets"));
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // 5. Device request count/device_ids mutual exclusivity
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void DeviceRequest_CountAndDeviceIds_Warning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    deploy:
      resources:
        reservations:
          devices:
            - capabilities: [gpu]
              count: 2
              device_ids:
                - ""0""
                - ""1""
";
        var ex = ExpectValidationError(yaml);
        Assert.Contains("count", ex.Message);
        Assert.Contains("device_ids", ex.Message);
        Assert.Contains("mutually exclusive", ex.Message);
    }

    [Fact]
    public void DeviceRequest_OnlyCount_NoWarning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    deploy:
      resources:
        reservations:
          devices:
            - capabilities: [gpu]
              count: 2
";
        var result = Load(yaml);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("count") && w.Contains("device_ids") && w.Contains("mutually exclusive"));
    }

    [Fact]
    public void DeviceRequest_OnlyDeviceIds_NoWarning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    deploy:
      resources:
        reservations:
          devices:
            - capabilities: [gpu]
              device_ids:
                - ""0""
";
        var result = Load(yaml);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("count") && w.Contains("device_ids") && w.Contains("mutually exclusive"));
    }

    [Fact]
    public void Gpus_CountAndDeviceIds_Warning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    gpus:
      - capabilities: [gpu]
        count: 1
        device_ids:
          - ""0""
";
        var ex = ExpectValidationError(yaml);
        Assert.Contains("count", ex.Message);
        Assert.Contains("device_ids", ex.Message);
        Assert.Contains("mutually exclusive", ex.Message);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // 6. Watch path non-empty validation
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void WatchPath_Empty_Warning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    develop:
      watch:
        - path: """"
          action: rebuild
";
        var ex = ExpectValidationError(yaml);
        Assert.Contains("develop.watch", ex.Message);
        Assert.Contains("path", ex.Message);
        Assert.Contains("must not be empty", ex.Message);
    }

    [Fact]
    public void WatchPath_Missing_Warning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    develop:
      watch:
        - action: rebuild
";
        var ex = ExpectValidationError(yaml);
        Assert.Contains("develop.watch", ex.Message);
        Assert.Contains("path", ex.Message);
        Assert.Contains("must not be empty", ex.Message);
    }

    [Fact]
    public void WatchPath_NonEmpty_NoWarning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    develop:
      watch:
        - path: ./src
          action: rebuild
";
        var result = Load(yaml);
        Assert.DoesNotContain(result.Warnings, w => w.Contains("develop.watch") && w.Contains("must not be empty"));
    }

    [Fact]
    public void WatchPath_MultipleEntries_OneEmpty_Warning()
    {
        var yaml = @"
services:
  web:
    image: nginx
    develop:
      watch:
        - path: ./src
          action: rebuild
        - path: """"
          action: sync
          target: /app
";
        var ex = ExpectValidationError(yaml);
        Assert.Contains("develop.watch[1]", ex.Message);
        Assert.Contains("must not be empty", ex.Message);
    }

    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Validation skipped with SkipValidation
    // â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    [Fact]
    public void AllPreStructValidations_SkippedWhenSkipValidation()
    {
        var yaml = @"
services:
  web:
    image: nginx
    ports:
      - host_ip: not-an-ip
        target: 80
        published: ""8080""
    develop:
      watch:
        - path: """"
          action: rebuild
volumes:
  mydata:
    external: true
    driver: local
configs:
  myconfig:
    file: ./config.txt
    environment: MY_VAR
secrets:
  mysecret:
    file: ./secret.txt
    environment: MY_SECRET_VAR
";
        var result = ComposeLoader.LoadFromContent(yaml, options: new LoadOptions { SkipValidation = true });
        // No validation warnings when skipped
        Assert.DoesNotContain(result.Warnings, w => w.Contains("not a valid IP address"));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("not allowed"));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("mutually exclusive"));
        Assert.DoesNotContain(result.Warnings, w => w.Contains("must not be empty"));
    }
}
