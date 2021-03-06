﻿// Copyright (c) 2019 Google LLC.
// 
// Licensed under the Apache License, Version 2.0 (the "License"); you may not
// use this file except in compliance with the License. You may obtain a copy of
// the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
// WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
// License for the specific language governing permissions and limitations under
// the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bookshelf.Models;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Diagnostics.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace Bookshelf
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<Services.ImageUploader>(provider =>
                new Services.ImageUploader(Configuration["Bucket"]));
            // Choose a BookStoreBackend.
            BookStoreBackend backend = Enum.Parse<BookStoreBackend>(
                Configuration["BookStore"], ignoreCase: true);
            switch (backend)
            {
                case BookStoreBackend.InMemory:
                    services.AddEntityFrameworkInMemoryDatabase()
                        .AddDbContext<BookStoreDbContext>(options =>
                        options.UseInMemoryDatabase("InMemory"));
                    services.AddScoped<IBookStore, DbBookStore>();
                    break;
                case BookStoreBackend.Firestore:
                    services.AddSingleton<IBookStore>(provider =>
                        new FirestoreBookStore(
                            Configuration["FIRESTORE_PROJECT_ID"]
                            ?? GetProjectId()));
                    break;
                case BookStoreBackend.SqlServer:
                    services.AddEntityFrameworkSqlServer()
                        .AddDbContext<BookStoreDbContext>(options =>
                            options.UseSqlServer(Configuration.GetConnectionString("SqlServer")));
                    services.AddScoped<IBookStore, DbBookStore>();
                    break;
                case BookStoreBackend.Npgsql:
                    services.AddEntityFrameworkNpgsql()
                        .AddDbContext<BookStoreDbContext>(options =>
                            options.UseNpgsql(Configuration.GetConnectionString("Npgsql")));
                    services.AddScoped<IBookStore, DbBookStore>();
                    break;
                default:
                    throw new NotImplementedException(backend.ToString());
            }

            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseGoogleExceptionLogging();
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
                app.UseHttpsRedirection();
            }

            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }

        public static string GetProjectId()
        {
            GoogleCredential googleCredential = Google.Apis.Auth.OAuth2
                .GoogleCredential.GetApplicationDefault();
            if (googleCredential != null)
            {
                ICredential credential = googleCredential.UnderlyingCredential;
                ServiceAccountCredential serviceAccountCredential =
                    credential as ServiceAccountCredential;
                if (serviceAccountCredential != null)
                {
                    return serviceAccountCredential.ProjectId;
                }
            }
            return Google.Api.Gax.Platform.Instance().ProjectId;
        }
    }
}
