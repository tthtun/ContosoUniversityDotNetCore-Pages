﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using ContosoUniversity.Data;
using ContosoUniversity.Models;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace ContosoUniversity.Pages.Instructors
{
    public class CreateEdit : PageModel
    {
        private readonly IMediator _mediator;

        [BindProperty]
        public Command Data { get; set; }

        public CreateEdit(IMediator mediator) => _mediator = mediator;

        public async Task OnGetCreateAsync() => Data = await _mediator.Send(new Query());

        public async Task<IActionResult> OnPostCreateAsync()
        {
            await _mediator.Send(Data);

            return this.RedirectToPageJson(nameof(Index));
        }

        public async Task OnGetEditAsync(Query query) => Data = await _mediator.Send(query);

        public async Task<IActionResult> OnPostEditAsync()
        {
            await _mediator.Send(Data);

            return this.RedirectToPageJson(nameof(Index));
        }

        public class Query : IRequest<Command>
        {
            public int? Id { get; set; }
        }

        public class QueryValidator : AbstractValidator<Query>
        {
            public QueryValidator()
            {
                RuleFor(m => m.Id).NotNull();
            }
        }

        public class Command : IRequest<int>
        {
            public Command()
            {
                AssignedCourses = new List<AssignedCourseData>();
                CourseAssignments = new List<CourseAssignment>();
                SelectedCourses = new string[0];
            }

            public int? Id { get; set; }

            public string LastName { get; set; }
            [Display(Name = "First Name")]
            public string FirstMidName { get; set; }

            [DisplayFormat(DataFormatString = "{0:yyyy-MM-dd}")]
            public DateTime? HireDate { get; set; }

            [Display(Name = "Location")]
            public string OfficeAssignmentLocation { get; set; }

            [IgnoreMap]
            public string[] SelectedCourses { get; set; }

            [IgnoreMap]
            public List<AssignedCourseData> AssignedCourses { get; set; }
            public List<CourseAssignment> CourseAssignments { get; set; }

            public class AssignedCourseData
            {
                public int CourseID { get; set; }
                public string Title { get; set; }
                public bool Assigned { get; set; }
            }

            public class CourseAssignment
            {
                public int CourseID { get; set; }
            }
        }

        public class CommandValidator : AbstractValidator<Command>
        {
            public CommandValidator()
            {
                RuleFor(m => m.LastName).NotNull().Length(0, 50);
                RuleFor(m => m.FirstMidName).NotNull().Length(0, 50);
                RuleFor(m => m.HireDate).NotNull();
            }
        }

        public class MappingProfile : Profile
        {
            public MappingProfile()
            {
                CreateMap<Instructor, Command>();
                CreateMap<CourseAssignment, Command.CourseAssignment>();
            }
        }

        public class QueryHandler : IRequestHandler<Query, Command>
        {
            private readonly SchoolContext _db;
            private readonly IConfigurationProvider _configuration;

            public QueryHandler(SchoolContext db, IConfigurationProvider configuration)
            {
                _db = db;
                _configuration = configuration;
            }

            public async Task<Command> Handle(Query message, CancellationToken token)
            {
                Command model;
                if (message.Id == null)
                {
                    model = new Command();
                }
                else
                {
                    model = await _db.Instructors
                        //.Include(m => m.CourseAssignments)
                        //.ThenInclude(ca => ca.Course)
                        .Where(i => i.Id == message.Id)
                        .ProjectTo<Command>(_configuration)
                        .SingleOrDefaultAsync(token);
                }

                PopulateAssignedCourseData(model);

                return model;
            }

            private void PopulateAssignedCourseData(Command model)
            {
                var allCourses = _db.Courses;
                var instructorCourses = new HashSet<int>(model.CourseAssignments.Select(c => c.CourseID));
                var viewModel = allCourses.Select(course => new Command.AssignedCourseData
                {
                    CourseID = course.Id,
                    Title = course.Title,
                    Assigned = instructorCourses.Any() && instructorCourses.Contains(course.Id)
                }).ToList();
                model.AssignedCourses = viewModel;
            }

        }

        public class CommandHandler : IRequestHandler<Command, int>
        {
            private readonly SchoolContext _db;

            public CommandHandler(SchoolContext db) => _db = db;

            public async Task<int> Handle(Command message, CancellationToken token)
            {
                Instructor instructor;
                if (message.Id == null)
                {
                    instructor = new Instructor();
                    _db.Instructors.Add(instructor);
                }
                else
                {
                    instructor = await _db.Instructors
                        .Include(i => i.OfficeAssignment)
                        .Include(i => i.CourseAssignments)
                        .Where(i => i.Id == message.Id)
                        .SingleAsync();
                }

                var courses = await _db.Courses.ToListAsync();

                instructor.Handle(message, courses);

                await _db.SaveChangesAsync();

                return instructor.Id;
            }
        }
    }
}