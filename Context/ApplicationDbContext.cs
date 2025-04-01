using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using FaceBookEngagement.Models;
using Microsoft.EntityFrameworkCore;

namespace FaceBookEngagement.Context
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Define DbSets for your entities
        public DbSet<Settings> Settings { get; set; }
        public DbSet<ClientDetail> ClientDetails { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Configure entity properties and relationships here
        }
    }
}

