export interface Project {
  id: string;
  name: string;
  createdAtUtc: string;
}

export interface CreateProjectRequest {
  name: string;
}
