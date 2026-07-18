using Amazon.CDK;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KairosCdk {
    sealed class Program {
        public static void Main(string[] args) {
            string appName = System.Environment.GetEnvironmentVariable("APP_NAME") ?? throw new ArgumentNullException("APP_NAME");
            string account = System.Environment.GetEnvironmentVariable("ACCOUNT_AWS") ?? throw new ArgumentNullException("ACCOUNT_AWS");
            string region = System.Environment.GetEnvironmentVariable("REGION_AWS") ?? throw new ArgumentNullException("REGION_AWS");

            var app = new App();
            _ = new KairosCdkStack(app, $"Cdk{appName}", new StackProps {
                Env = new Amazon.CDK.Environment {
                    Account = account,
                    Region = region,
                }
            });
            app.Synth();
        }
    }
}
