using FinLens.Domain.Entities;
using FinLens.Infrastructure.Persistence;

namespace FinLens.API.GraphQL.Types;

public class WorkspaceType : ObjectType<Workspace>
{
    protected override void Configure(IObjectTypeDescriptor<Workspace> descriptor)
    {
        descriptor.Field(w => w.Id);
        descriptor.Field(w => w.Name);
        descriptor.Field(w => w.Type);
        descriptor.Field(w => w.CreatedAt);
        descriptor.Field(w => w.Members)
            .UseDbContext<ApplicationDbContext>()
            .UseFiltering()
            .UseSorting();
    }
}