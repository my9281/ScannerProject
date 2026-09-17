namespace Scanner.Server.Model;

public sealed record Order(
    int Id,
    string Dtype,
    string Sn,
    string Doid,
    DateTime Dtime,
    string Dnote);

public sealed record SaveOrderRequest(
    string Dtype,
    string Sn,
    string Doid,
    DateTime Dtime,
    string Dnote);
