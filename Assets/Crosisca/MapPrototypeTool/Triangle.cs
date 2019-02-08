struct Triangle
{
    public int vertexIndexA;
    public int vertexIndexB;
    public int vertexIndexC;

    readonly int[] vertices;

    public Triangle (int vertexIndexA, int vertexIndexB, int vertexIndexC)
    {
        this.vertexIndexA = vertexIndexA;
        this.vertexIndexB = vertexIndexB;
        this.vertexIndexC = vertexIndexC;

        vertices = new[] {this.vertexIndexA, this.vertexIndexB, this.vertexIndexC};
    }

    public int this[int i]
    {
        get { return vertices[i]; }
    }

    public bool Contains(int vertexIndex)
    {
        return vertexIndex == vertexIndexA || vertexIndex == vertexIndexB || vertexIndex == vertexIndexC;
    }
}
