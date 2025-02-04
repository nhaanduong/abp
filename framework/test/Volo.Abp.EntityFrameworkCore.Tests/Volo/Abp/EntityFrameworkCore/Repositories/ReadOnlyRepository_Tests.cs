using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.TestApp.Domain;
using Volo.Abp.TestApp.EntityFrameworkCore;
using Volo.Abp.TestApp.Testing;
using Volo.Abp.Uow;
using Xunit;

namespace Volo.Abp.EntityFrameworkCore.Repositories;

public class ReadOnlyRepository_Tests : TestAppTestBase<AbpEntityFrameworkCoreTestModule>
{
    [Fact]
    public async Task ReadOnlyRepository_Should_NoTracking()
    {
        // Non-read-only repository tracking default
        await WithUnitOfWorkAsync(async () =>
        {
            var repository = GetRequiredService<IRepository<Person, Guid>>();
            var db = await repository.GetDbContextAsync();
            db.ChangeTracker.Entries().Count().ShouldBe(0);
            var list = await repository.GetListAsync();
            list.Count.ShouldBeGreaterThan(0);
            db.ChangeTracker.Entries().Count().ShouldBe(list.Count);
        });

        // Read-only repository no tracking default
        await WithUnitOfWorkAsync(async () =>
        {
            var readonlyRepository = GetRequiredService<IReadOnlyRepository<Person, Guid>>();
            var db = await readonlyRepository.GetDbContextAsync();
            db.ChangeTracker.Entries().Count().ShouldBe(0);
            var list = await readonlyRepository.GetListAsync();
            list.Count.ShouldBeGreaterThan(0);
            db.ChangeTracker.Entries().Count().ShouldBe(0);
        });

        // Read-only repository can tracking manually by AsTracking
        await WithUnitOfWorkAsync(async () =>
        {
            var readonlyRepository = GetRequiredService<IReadOnlyRepository<Person, Guid>>();
            var db = await readonlyRepository.GetDbContextAsync();
            db.ChangeTracker.Entries().Count().ShouldBe(0);
            var list = await (await readonlyRepository.ToEfCoreRepository().GetQueryableAsync()).AsTracking().ToListAsync();
            list.Count.ShouldBeGreaterThan(0);
            db.ChangeTracker.Entries().Count().ShouldBe(list.Count);
        });
    }

    [Fact]
    public async Task ReadOnlyRepository_Should_Throw_AbpRepositoryIsReadOnlyException_When_Write_Method_Call()
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var repository = GetRequiredService<IRepository<Person, Guid>>();
            await repository.ToEfCoreRepository().InsertAsync(new Person(Guid.NewGuid(), "test", 18));
            var person = await repository.ToEfCoreRepository().FirstOrDefaultAsync();
            person.ShouldNotBeNull();
        });

        await WithUnitOfWorkAsync(async () =>
        {
            await Assert.ThrowsAsync<AbpRepositoryIsReadOnlyException>(async () =>
            {
                var readonlyRepository = GetRequiredService<IReadOnlyRepository<Person, Guid>>();
                await readonlyRepository.ToEfCoreRepository().As<EfCoreRepository<TestAppDbContext, Person, Guid>>().InsertAsync(new Person(Guid.NewGuid(), "test readonly", 18));
            });
        });
    }

    [Fact]
    public async Task ReadOnlyRepository_Should_NoTracking_In_UOW()
    {
        var repository = GetRequiredService<IRepository<Person, Guid>>();
        var readonlyRepository = GetRequiredService<IReadOnlyRepository<Person, Guid>>();

        await WithUnitOfWorkAsync(async () =>
        {
            await repository.InsertAsync(new Person(Guid.NewGuid(), "people1", 18));
            await repository.InsertAsync(new Person(Guid.NewGuid(), "people2", 19));
        });

        using (var uow = GetRequiredService<IUnitOfWorkManager>().Begin())
        {
            var p1 = await repository.FirstOrDefaultAsync(x => x.Name == "people1");
            p1.ShouldNotBeNull();
            p1.ChangeName("people1-updated");

            var p2 = await readonlyRepository.FirstOrDefaultAsync(x => x.Name == "people2");
            p2.ShouldNotBeNull();
            p2.ChangeName("people2-updated");

            await uow.CompleteAsync();
        }

        await WithUnitOfWorkAsync(async () =>
        {
            (await repository.FirstOrDefaultAsync(x => x.Name == "people1")).ShouldBeNull();
            (await repository.FirstOrDefaultAsync(x => x.Name == "people1-updated")).ShouldNotBeNull();

            (await readonlyRepository.FirstOrDefaultAsync(x => x.Name == "people2")).ShouldNotBeNull();
            (await readonlyRepository.FirstOrDefaultAsync(x => x.Name == "people2-updated")).ShouldBeNull();
        });
    }
}
