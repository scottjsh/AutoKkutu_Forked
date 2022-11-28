﻿// <auto-generated />
using AutoKkutu.Databases;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace AutoKkutu.Migrations
{
    [DbContext(typeof(PathDbContext))]
    partial class PathDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "7.0.0");

            modelBuilder.Entity("AutoKkutu.Databases.WordIndexModel", b =>
                {
                    b.Property<string>("Index")
                        .HasMaxLength(2)
                        .HasColumnType("TEXT")
                        .HasColumnName("word_index");

                    b.HasKey("Index");

                    b.ToTable("WordIndexModel");
                });

            modelBuilder.Entity("AutoKkutu.Databases.WordModel", b =>
                {
                    b.Property<int>("SequenceId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER")
                        .HasColumnName("seq");

                    b.Property<int>("Flags")
                        .HasColumnType("INTEGER")
                        .HasColumnName("flags");

                    b.Property<string>("KkutuWorldIndex")
                        .IsRequired()
                        .HasMaxLength(2)
                        .HasColumnType("TEXT")
                        .HasColumnName("kkutu_index");

                    b.Property<string>("ReverseWordIndex")
                        .IsRequired()
                        .HasMaxLength(1)
                        .HasColumnType("TEXT")
                        .HasColumnName("reverse_word_index");

                    b.Property<string>("Word")
                        .IsRequired()
                        .HasMaxLength(256)
                        .HasColumnType("TEXT")
                        .HasColumnName("word");

                    b.Property<string>("WordIndex")
                        .IsRequired()
                        .HasMaxLength(1)
                        .HasColumnType("TEXT")
                        .HasColumnName("word_index");

                    b.HasKey("SequenceId");

                    b.ToTable("Word");
                });
#pragma warning restore 612, 618
        }
    }
}
