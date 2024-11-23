# YARP API Gateway

YARP(Yet Another Reverse Proxy), Microsoft tarafından geliştirilen open source bir API Gateway ve reverse proxy kütüphanesidir.

### 1. Kurulum ve Yapılandırma:

* **Proje Oluşturma:**  YARP'ın kurulumu için öncelikle API Gateway görevi görecek bir Asp.NET Core uygulaması oluşturulmalı ve ardından bu uygulamaya YARP kütüphanesi yüklenmelidir.

* **NuGet Paketinin Eklenmesi:**  `Yarp.ReverseProxy` paketini NuGet Paket Yöneticisi aracılığıyla ekleyin.

* **YARP Yapılandırılması:**  Ardından API Gateway görevi görecek bu uygulama içerisinde YARP'ı yapılandırıyoruz. Bunun için `appsettings.json` içerisinde aşağıdaki gibi yapılandırılmada bulunulması gerekmektedir.

  ```json
  "ReverseProxy": {
    "Routes": {
      "Cluster1": {
        "ClusterId": "Cluster1",
        "Match": {
          "Path": "/api1/{**catch-all}"
        },
        "Transforms": [
          {
            "RequestHeader": "api1-request-header",
            "Append": "api1 request"
          },
          {
            "ResponseHeader": "api1-response-header",
            "Append": "api1 response",
            "When": "Always"
          }
        ]
      },
    },
    "Clusters": {
      "Cluster1": {
        "Destinations": {
          "destination1": {
            "Address": "https://localhost:7289"
          }
        }
      }
    }
  }
  ```

  * **Açıklama:**
      * `Clusters`: Reverse proxy tarafından yönlendirilen isteklerin hedeflerini belirleyebilmek için birden fazla hedef sunucu veya hizmet gruplandırılmaktadır. Her bir cluster, içerisinde hedef sunucunun ya da servisin adresini belirten destination barındırmaktadır ve bunların sayısı bir veya birden fazla olabilmektedir. Böylece istekler belirli bir küme içindeki farklı hedeflere yönlendirilebilir, bu da hedef sunucularda çeşitlilik sunabileceği gibi load balancing vs. gibi imkanlarda ekstradan avantajlar sağlayabilmektedir.
      * `Routes`:  İsteklerin reverse proxy tarafından yönlendirilmesinin hangi kurala, cluster'a ve hedefe göre yölendirileceği tanımlarını barındırır. 

### 2. Ocelot'un Entegre Edilmesi:

YARP yapılandırıldıktan sonra, oluşturmuş olduğumuz yapılandırma dosyasının uygulamaya dahil edilmesi gerekmektedir. `Program.cs` dosyanızı aşağıdaki gibi düzenleyin:

```csharp
var builder = WebApplication.CreateBuilder(args);
  
builder.Services.AddReverseProxy()
  .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));
  
var app = builder.Build();
  
app.MapReverseProxy();
  
app.Run();
```

Burada görüldüğü üzere `AddReverseProxy` metoduyla YARP için gerekli servisi uygulamaya ekliyor, `MapReverseProxy` middleware'i ile de reverse proxy özelliğini uygulamada aktifleştiriyoruz.

YARP yapılandırmasını `LoadFromConfig` metodunun dışında `LoadFromMemory` metodu eşliğinde in-memory üzerinden de aşağıdaki gibi konfigure edebiliriz;

```csharp
builder.Services.AddReverseProxy()
    .LoadFromMemory(new List<RouteConfig>
    {
        new RouteConfig
        {
             RouteId = "Cluster1-Route",
             ClusterId  = "Cluster1",
             Match = new()
             {
                  Path = "/api1/{**catch-all}"
             },
             Transforms = new List<Dictionary<string, string>>
             {
                new Dictionary<string, string>()
                {
                    ["RequestHeader"] = "api1-request-header",
                    ["Append"] = "api1 request"
                },
                   new()
                {
                    { "ResponseHeader", "api1-response-header" },
                    { "Append", "api1 response" },
                    { "When", "Always" },
                }
             }
        }
    }, new List<ClusterConfig>
    {
        new ClusterConfig()
        {
            ClusterId = "Cluster1",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["destination1"] = new(){ Address = "https://localhost:7289"}
            }
        }
    });
```

### 3. Mikro Servislerin Çalıştırılması:

YARP'ın yönlendirme yapması için, `appsettings.json` içerisinde ya da in-memory'de tanımlanan mikro servislerin çalışır durumda olması gerekir.  Bu servisler ayrı projeler olabilir ve bağımsız olarak çalıştırılabilirler.

### 4. Authentication & Authentication:

YARP'ta authentication ve authorization işlemlerini gerçekleştirmek için genellikle JWT kullanılmaktadır. Bunun için `Microsoft.AspNetCore.Authentication.JwtBearer` kütüphanesinin uygulamaya yüklenmesi gerekmektedir.

Bu servise JWT kullanarak API Gateway üzerinden istekte bulunabilmek için YARP'ın kullanıldığı servistede aşağıdaki gibi authentication ve authorization yapılandırmasında bulunulmasi gerekmektedir.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
            };
        });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();

app.Run();
```

Audience, Issuer ve SecurityKey değerlerinin `appsettings.json` içerisinde saklamak daha doğru olacaktır. Bu değerleri kendinize göre özelleştirebilirsiniz.


Son olarak tanımlanmış olan bu politikanın hangi hedef servisle ilişkili olduğunu ifade edebilmek için bunu yapılandırmada aşağıdaki gibi AuthorizationPolicy eşliğinde bildirmek gerekmektedir.

  ```json
  "ReverseProxy": {
    "Routes": {
      "Cluster1": {
        "ClusterId": "Cluster1",
        "AuthorizationPolicy": "Authenticated",
        "Match": {
          "Path": "/api1/{**catch-all}"
        },
        "Transforms": [
          {
            "RequestHeader": "api1-request-header",
            "Append": "api1 request"
          },
          {
            "ResponseHeader": "api1-response-header",
            "Append": "api1 response",
            "When": "Always"
          }
        ]
      },
    },
    "Clusters": {
      "Cluster1": {
        "Destinations": {
          "destination1": {
            "Address": "https://localhost:7289"
          }
        }
      }
    }
  }
  ```

Böylece API Gateway uygulamasında authentication ve authorization'ı yapılandırmış bulunuyoruz. Bu yapılandırmalara uygun bir şekilde servislere erişim göstermek isteniyorsa eğer alt servislerde de benzer şekilde  yapılandırmada bulunulması gerekmektedir.

### 5. YARP ile Load Balancing:

  ```json
  "ReverseProxy": {
    "Routes": {
      "Cluster1": {
        "ClusterId": "Cluster1",
        "AuthorizationPolicy": "Authenticated",
        "Match": {
          "Path": "/api1/{**catch-all}"
        },
        "Transforms": [
          {
            "RequestHeader": "api1-request-header",
            "Append": "api1 request"
          },
          {
            "ResponseHeader": "api1-response-header",
            "Append": "api1 response",
            "When": "Always"
          }
        ]
      },
    },
    "Clusters": {
      "Cluster1": {
        "Destinations": {
          "destination1": {
            "Address": "https://localhost:7222"
          },
          "destination2": {
            "Address": "https://localhost:7223"
          },
          "destination3": {
            "Address": "https://localhost:7224"
          }
        },
        "LoadBalancing": {
          "Policy": "RoundRobin"
        }
      }
    }
  }
  ```
