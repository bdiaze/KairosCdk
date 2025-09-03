using ApiCalendarizarProcesos.Models;

namespace ApiCalendarizarProcesos.Endpoints {
    public static class TodosEndpoints {
        
        private static Todo[] sampleTodos = new Todo[] {
            new(1, "Walk the dog"),
            new(2, "Do the dishes", DateOnly.FromDateTime(DateTime.Now)),
            new(3, "Do the laundry", DateOnly.FromDateTime(DateTime.Now.AddDays(1))),
            new(4, "Clean the bathroom"),
            new(5, "Clean the car", DateOnly.FromDateTime(DateTime.Now.AddDays(2)))
        };

        public static IEndpointRouteBuilder MapTipoMensajesEndpoints(this IEndpointRouteBuilder routes) {
            RouteGroupBuilder group = routes.MapGroup("/todos");
            group.MapGetEndpoint();
            group.MapGetByIdEndpoint();

            return routes;
        }

        private static IEndpointRouteBuilder MapGetEndpoint(this IEndpointRouteBuilder routes) {
            routes.MapGet("/", () => sampleTodos);
            
            return routes;
        }

        private static IEndpointRouteBuilder MapGetByIdEndpoint(this IEndpointRouteBuilder routes) {
            routes.MapGet("/{id}", (int id) =>
                sampleTodos.FirstOrDefault(a => a.Id == id) is { } todo
                    ? Results.Ok(todo)
                    : Results.NotFound());

            return routes;
        }
    }
}
